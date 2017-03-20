import sys, getopt, json, subprocess, os, re


def find_project_version(manifest, project_name):
    for project in manifest["ProjectVersions"]:
        if project["Name"] == project_name:
            return project["Version"]

    return None


def get_tickets_for_project(projects_path, project_name, start_tag, end_tag):
    if start_tag == end_tag:
        return []

    command = "git --no-pager log --date-order --grep=\"Merge\" --invert-grep --pretty=format:\" % s----__---- -% " \
              "h----__---- -% aI----__---- -% an\" release/" + start_tag + "...release/" + end_tag
    command_cwd = os.path.join(projects_path, project_name)
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=None, shell=True, cwd=command_cwd)
    tags = []

    for line in process.stdout.readlines():
        match = re.search("[A-Z]+[-_]\\d+", line.decode("utf-8"))
        if match is not None:
            tags.append(match.group(0))

    process.wait()

    return tags


def get_tickets_for_manifest(projects_path, projects, current_manifest, previous_manifest):
    manifest_tickets = []

    for project in projects:
        current_version = find_project_version(current_manifest, project)
        previous_version = find_project_version(previous_manifest, project)

        if current_version is None or previous_version is None:
            continue

        project_tickets = get_tickets_for_project(projects_path, project, previous_version, current_version)
        manifest_tickets.extend(project_tickets)

    return list(set(manifest_tickets))  # Remove duplicates :)


def main(argv):

    projects_path = ""
    projects = []
    output = []

    try:
        opts, argv = getopt.getopt(argv, "d:p:")
    except getopt.GetoptError:
        print("find-tickets-for-manifests.py -d <path-to-projects> -p <projects-json>")
        sys.exit(2)

    for opt, arg in opts:
        if opt == "-d":
            projects_path = arg
            if not os.path.isabs(projects_path):
                projects_path = os.path.join(os.getcwd(), projects_path)

        elif opt == "-p":
            projects = json.loads(arg)

    if sys.version_info < (3, 0):
        manifests_str = raw_input()
    else:
        manifests_str = input()

    manifests = json.loads(manifests_str)

    for manifest in manifests:
        current_manifest = manifest["CurrentManifest"]
        previous_manifest = manifest["PreviousManifest"]

        output.append({
            "manifestName": current_manifest["Name"],
            "tickets": get_tickets_for_manifest(projects_path, projects, current_manifest, previous_manifest)
        })

    print(json.dumps(output))


if __name__ == "__main__":
    main(sys.argv[1:])

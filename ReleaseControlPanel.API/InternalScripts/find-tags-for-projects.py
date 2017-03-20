import sys, getopt, json, subprocess, os, re


def get_tags_for_project(projects_path, project_name):
    command = "git tag --sort=v:refname"
    command_cwd = os.path.join(projects_path, project_name)
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=None, shell=True, cwd=command_cwd)
    tags = []

    for line in process.stdout.readlines():
        match = re.search("release/(\\d+\\.\\d+(?:\\.\\d+)?)", line.decode("utf-8"))
        if match is not None:
            tags.append(match.group(1))

    process.wait()

    return tags


def main(argv):

    projects_path = ""
    projects = []
    output = []

    try:
        opts, argv = getopt.getopt(argv, "d:p:")
    except getopt.GetoptError:
        print("find-tags-for-projects.py -d <path-to-projects> -p <projects-array-json>")
        sys.exit(2)

    for opt, arg in opts:
        if opt == "-d":
            projects_path = arg
            if not os.path.isabs(projects_path):
                projects_path = os.path.join(os.getcwd(), projects_path)

        elif opt == "-p":
            projects = json.loads(arg)

    for project_name in projects:
        output.append({
            "projectName": project_name,
            "tags": get_tags_for_project(projects_path, project_name)
        })

    print(json.dumps(output))


if __name__ == "__main__":
    main(sys.argv[1:])

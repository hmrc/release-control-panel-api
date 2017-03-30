# ReleaseControlPanel.API
[![Build Status](https://travis-ci.org/hmrc/release-control-panel-api.svg?branch=master)](https://travis-ci.org/hmrc/release-control-panel-api) [ ![Download](https://api.bintray.com/packages/hmrc/releases/release-control-panel-api/images/download.svg) ](https://bintray.com/hmrc/releases/release-control-panel-api/_latestVersion)

This is the API for Release Control Panel tool.
It exposes API endpoints for the frontend to communicate with.
Here you can find the UI to this tool: [https://github.com/hmrc/release-control-panel-ui](https://github.com/hmrc/release-control-panel-ui)

## Requirements
1. To run this tool you need to have access to HMRC VPN. You can find the information on how to set it up in the HMRC confluence.

2. You also need to setup SSH keys to access HMRC's Github Enterprise.
In order to do that you will need to follow these steps:
   - [generate a new SSH key](https://help.github.com/articles/generating-a-new-ssh-key-and-adding-it-to-the-ssh-agent/#generating-a-new-ssh-key).
     Please skip the **Adding your SSH key to the ssh-agent** step cos I found it never works as it should.
   - [create a .ssh config file](http://nerderati.com/2011/03/17/simplify-your-life-with-an-ssh-config-file/) to make sure your system knows about it.
   
3. Have a local instance of mongodb.
   There are two ways of getting mongodb working on your machine. One is to locally install the MongoDB from [https://www.mongodb.com/download-center#community](https://www.mongodb.com/download-center#community). Second (in my opinion better way) is to install it through a docker machine and just expose the mongodb port to the host.

4. Download and install [latest version of .NET Core](https://www.microsoft.com/net/core)

5. Make sure your `git` version is at least 2.4.0. Best if you update it to the latest before running the tool: https://git-scm.com/
   To check the version of git open new command line / terminal window and run `git --version`

6. Make sure you have python installed on your machine. The tool supports both 2.7 and > 3 versions of python.

Before running the application for the first time you have to configure the application.

## Configuration and first run
Before running the tool for the first time you have to modify it's configuration to match your team's requirements.
The configuration file is located in `ReleaseControlPanel.API/appsettings.json`. If you're a member of HRMC Digital you can try looking for ready examples of configuration file in HMRC confluence.

After that you have to instruct .NET to restore external packages.
To do that open a new terminal / command line window, navigate to the `ReleaseControlPanel.API/` directory within project's root and run following command:
```bash
dotnet restore
```


## Starting the API
Navigate to the `ReleaseControlPanel.API/` directory of the project and run `dotnet run`

You should be greeted wtih following message:
```
Now listening on: http://localhost:5000
Application started. Press Ctrl+C to shut down.
```

### License
This code is open source software licensed under the [Apache 2.0 License]("http://www.apache.org/licenses/LICENSE-2.0.html").

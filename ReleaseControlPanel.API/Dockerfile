FROM microsoft/aspnetcore:1.1
ARG source

# Installing latest version of GIT CLI
RUN apt-key adv --keyserver keyserver.ubuntu.com --recv-keys A1715D88E1DF1F24
RUN echo "deb http://ppa.launchpad.net/git-core/ppa/ubuntu precise main" >> /etc/apt/sources.list
RUN echo "deb-src http://ppa.launchpad.net/git-core/ppa/ubuntu precise main" >> /etc/apt/sources.list
RUN apt-get update && apt-get install -y git python

# Registering git .ssh certificates
COPY ./.ssh /root/.ssh
RUN cd ~/ && chmod 700 -R .ssh && chmod 777 -R /tmp

WORKDIR /app

COPY ${source:-obj/Docker/publish} .
COPY ./InternalScripts ./InternalScripts

ENV MongoDb:ConnectionString mongodb://mongodb:27017/

EXPOSE 80
ENTRYPOINT ["dotnet", "ReleaseControlPanel.API.dll"]
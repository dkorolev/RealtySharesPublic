#!/bin/bash

# PREREQUISITES: Environment setup.

CHEATSHEET="""
# nginx, git, mono, and F#.
sudo apt-get install nginx git mono-complete fsharp

# HTTPS certs for nuget.exe and mono itself.
mozroots --import --sync

# NuGet, the .NET package manager. The .exe runs from under mono.
# TODO(dkorolev+devops): This is insecure, put the right version into a secure location.
wget http://nuget.org/nuget.exe
chmod +x nuget.exe
nuget.exe update -self
sudo mv nuget.exe /usr/bin/
"""


# PRODUCTION: Update NuGet packages and build the solution.

# Install NuGet packages for each F# project in current and sibling directories with F# projects.
(cd .. ;
 mkdir packages;
 for dir in $(ls */*.fsproj | cut -f1 -d '/' | sort -u) ; do
   cd packages;
   for i in $(cat ../$dir/packages.config | grep "package id=" | cut -f2 -d'"') ; do
     nuget.exe install $i
   done
 done)

# Bulid the solution.
xbuild


# DONE: Now, the executables can be run `fsharpi` should be able to run `.fsx` scripts smoothly.

# Uncomment the following line to try.
# fsharpi Scratchpad.fsx

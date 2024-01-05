#!/bin/bash

# Variables
REPO_URL=https://github.com/dotnet/aspnetcore.git
PATH_IN_REPO=src/Shared/CertificateGeneration/
TEMP_DIR=$(mktemp -d)

# Clone the repository to a temporary directory
git clone $REPO_URL $TEMP_DIR

# Copy files from the specified path to the current directory
cp -r $TEMP_DIR/$PATH_IN_REPO/* .

# Cleanup: Delete the temporary directory
rm -rf $TEMP_DIR

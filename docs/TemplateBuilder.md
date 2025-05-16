# User Manual for Template Extraction and Project Creation in Azure DevOps

## Overview

This document provides step-by-step instructions on how to extract a template from an existing Azure DevOps project and use the extracted template to create a new project.

---

## Prerequisites

1. Ensure the following are installed on your machine:
   - [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
   - [Visual Studio](https://visualstudio.microsoft.com/) or any other preferred IDE.
2. Ensure you have access to an Azure DevOps organization and the necessary permissions to create projects and extract templates.
3. Have a valid Azure DevOps Personal Access Token (PAT) or be ready to authenticate using Azure AD.

---

## Template Extraction

### Step 1: Start the Application
1. Run the `ADOGenerator` application.
2. You will see the following prompt:
   ```
   Welcome to Azure DevOps Demo Generator! This tool will help you generate a demo environment for Azure DevOps.
   ```

### Step 2: Select Template Extraction
1. Choose the option to generate new artifacts using an existing project:
   ```
   Do you want to create a new template or create a new project using the demo generator project template?
   1. Create a new project using the demo generator project template
   2. Generate new artifacts using an existing project.
   Enter the option number from the list of options above:
   ```
2. Enter `2` and press `Enter`.

### Step 3: Authenticate
1. Choose your authentication method:
   ```
   Choose authentication method: 1. Device Login using AD auth 2. Personal Access Token (PAT)
   ```
2. Enter `1` for Azure AD authentication or `2` for PAT authentication.
   - For PAT authentication:
     - Enter your Azure DevOps organization name.
     - Enter your PAT when prompted.

### Step 4: Select an Existing Project
1. The application will list the available projects in your Azure DevOps organization.
2. Select the project you want to extract the template from.

### Step 5: Analyze and Generate Artifacts
1. The application will analyze the selected project and prompt:
   ```
   Do you want to create artifacts yes/no:
   ```
2. Enter `yes` to generate the artifacts.
3. The application will extract the following:
   - Work items
   - Build and release definitions
   - Iterations
   - Teams
   - Service endpoints
4. The extracted artifacts will be saved in the `Templates` directory under a folder named `CT-<ProjectName>`.

---

## Using the Extracted Template to Create a New Project

### Step 1: Start the Application
1. Run the `ADOGenerator` application.
2. You will see the following prompt:
   ```
   Welcome to Azure DevOps Demo Generator! This tool will help you generate a demo environment for Azure DevOps.
   ```

### Step 2: Select Project Creation
1. Choose the option to create a new project:
   ```
   Do you want to create a new template or create a new project using the demo generator project template?
   1. Create a new project using the demo generator project template
   2. Generate new artifacts using an existing project.
   Enter the option number from the list of options above:
   ```
2. Enter `1` and press `Enter`.

### Step 3: Authenticate
1. Choose your authentication method:
   ```
   Choose authentication method: 1. Device Login using AD auth 2. Personal Access Token (PAT)
   ```
2. Enter `1` for Azure AD authentication or `2` for PAT authentication.
   - For PAT authentication:
     - Enter your Azure DevOps organization name.
     - Enter your PAT when prompted.

### Step 4: Select a Template
1. The application will display the available templates in the `Templates` directory.
2. Select the template you want to use by entering its corresponding number.

### Step 5: Enter Project Details
1. Enter the name of the new project when prompted:
   ```
   Enter the new project name:
   ```

### Step 6: Confirm Extension Installation
1. If the selected template requires extensions, the application will display the required extensions and ask for confirmation:
   ```
   Do you want to proceed with this extension? (yes/No): press enter to confirm
   ```

### Step 7: Create the Project
1. The application will create the project in Azure DevOps using the selected template.
2. Once the project is created, you will see the following message:
   ```
   Project created successfully.
   ```

---

## Additional Notes

- If you encounter any issues during template extraction or project creation, check the console output for error messages.
- Ensure that the `TemplateSetting.json` file is present in the `Templates` directory for the application to load templates.
- If you skip updating the template settings, you can manually copy the generated artifacts and update the `TemplateSetting.json` file.

---

## Support

For further assistance, refer to the SUPPORT.md file in the repository or contact the project maintainers.
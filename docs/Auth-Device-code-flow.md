### **1. Register Your Application in Azure AD**

1. **Sign in to the Azure Portal**  
   Navigate to [Azure Portal](https://portal.azure.com).

2. **Register a New Application**
   - Go to **Azure Active Directory** > **App registrations** > **New registration**.
   - Enter the following details:
     - **Name**: Enter a meaningful name for your app.
     - **Supported Account Types**: Choose an option based on your needs:
       - Single tenant: Accounts in your organization only.
       - Multi-tenant: Accounts in any organization's directory.
     - **Redirect URI**: This is not required for Device Code Flow but can be added later if needed.
   - Click **Register**.

3. **Copy the Application (Client) ID**
   - After registration, go to the **Overview** section.
   - Copy the **Application (client) ID** and the **Directory (tenant) ID** and save it for later.
    
    ![image](/docs/Images/AppDetails.png)

4. **Configure API Permissions**
   - Navigate to **API Permissions** > **Add a permission**.
   - Select **Azure DevOps** or any other API you want to access.

    ![image](/docs/Images/ChooseAPI.png)

   - Choose **Delegated permissions**

   - Add the required scopes (e.g., `User.Read`).

5. **Following are the scopes required.**
   
    | Scope                     | Description                              | 
    |---------------------------|------------------------------------------|
    | vso.agentpools            | Agent Pools (read)                       | 
    | vso.build_execute         | Build (read and execute)                 | 
    | vso.code_full             | Code (full)                              | 
    | vso.dashboards_manage     | Team dashboards (manage)                 | 
    | vso.extension_manage      | Extensions (read and manage)             | 
    | vso.profile               | User profile (read)                      | 
    | vso.project_manage        | Project and team (read, write and manage)| 
    | vso.release_manage        | Release (read, write, execute and manage)| 
    | vso.serviceendpoint_manage| Service Endpoints (read, query and manage)|
    | vso.test_write            | Test management (read and write)         | 
    | vso.variablegroups_write  | Variable Groups (read, create)           | 
    | vso.work_full             | Work items (full)                        | 

---

### **2. Configure the App Settings**
1. Open your application’s configuration file (e.g., `appsettings.json`) under AppSettings.
2. Add the following details:
   ```json
   {
     "AppSettings": {
        "...": "...",
       "clientId": "<Your Application (Client) ID>",
       "tenantId": "<Your Directory (Tenant) ID>",
       "scope": "499b84ac-1321-427f-aa17-267ca6975798/.default"
     }
   }
   ```
   Replace placeholders with the actual values from the Azure Portal.

---

### **3. Test the Application**
1. Run your application.
2. The app will display a message instructing the user to go to `https://microsoft.com/devicelogin` and enter the provided device code.
3. After entering the code, users will authenticate, and the app will receive an access token.


# Snyk webhook subscription

This repository contains some examples on how to subscribe to Snyk notifications and process the information in order to forward these notifications to Microsoft Teams or Azure DevOps Boards.

Steps you need to follow in order to set-up this integration:

1. Create a Snyk Webhook

```json
POST https://snyk.io/api/v1/org/{SNYK-ORG-ID}/webhooks HTTP/2
Host: snyk.io
Authorization: token {SNYK-TOKEN}
Content-Type: application/json

{
    "url": "https://{URL}",
    "secret": "my-secret-string"
}
```

As a result, you will get a response like this:

```json
{
  "id": "{SNYK-WEBHOOK-ID}",
  "url": "https://{URL}",
}
```

You could then use the Snyk Ping API in order to pro-actively trigger the webhook in order to test your integration:

```json
POST https://snyk.io/api/v1/org/{SNYK-ORG-ID}/webhooks/{SNYK-WEBHOOK-ID}/ping HTTP/2
Host: snyk.io
Authorization: token {SNYK-TOKEN}
Content-Type: application/json
```

Once this webhook is created, you can the continue to the next step.

2. Create an Azure Function App in order to receive the webhook from Snyk

I provided this [sample Azure Function](azure-function-azure-boards.cs) code written in C# in order to process the payload from Snyk and send it to an Azure DevOps Board.

This Azure Function requires the following environment variables to be set-up in order for work items to be created in Azure DevOps Boards:

- AZURE_DEVOPS_ORG: the name of the Azure DevOps organisation
- AZURE_DEVOPS_PROJECT: the Azure DevOps project to create work items for
- AZURE_DEVOPS_USER: the Azure DevOps user name
- AZURE_DEVOPS_PAT: the Azure DevOps personall access token
- AZURE_DEVOPS_API_VERSION: the Azure DevOps API version to use, e.g. "7.1-preview.3"

3. Based on the notifications settings in your Snyk account, you will then be notified of new issues in your repositories

![](/azure-devops.boards.png)
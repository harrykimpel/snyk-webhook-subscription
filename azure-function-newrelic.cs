#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Web;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    log.LogInformation("C# HTTP trigger function processed a request.");

    string name = req.Query["name"];

    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    dynamic data = JsonConvert.DeserializeObject(requestBody);
    //log.LogInformation("data: " + requestBody);

    string responseMessage = "No valid payload received!";

    try
    {
        if (data.project != null)
        {
            string count = data.newIssues.Count.ToString();
            string projectName = data.project.name;
            string[] projectNameParts = projectName.Split(':');
            string containerImage = projectName;
            if (projectNameParts.Length > 0)
            {
                containerImage = projectNameParts[1] + ":" + data.project.imageTag;
            }
            string repoURL = data.project.name;
            string entityLookupValue = repoURL;
            int idxRepoURLBranch = repoURL.IndexOf("(");
            if (data.project.origin == "github")
            {
                int idxRepoURLProject = repoURL.IndexOf(":");
                string package = "";
                if (idxRepoURLBranch >= 0)
                {
                    package = repoURL.Substring(idxRepoURLProject + 1, repoURL.Length - idxRepoURLProject - 1);
                    log.LogInformation("package: " + package);
                    entityLookupValue = "https://github.com/" + repoURL.Substring(0, idxRepoURLBranch);
                    repoURL = "https://github.com/" + repoURL.Substring(0, idxRepoURLBranch);

                    if (data.project.branch != "")
                    {
                        repoURL = repoURL + "/blob/" + data.project.branch + "/" + package;
                    }
                }
                else
                {
                    entityLookupValue = "https://github.com/" + repoURL.Substring(0, idxRepoURLProject);
                    repoURL = "https://github.com/" + repoURL.Substring(0, idxRepoURLProject);
                }
            }
            else if (data.project.origin == "docker-hub")
            {
                if (idxRepoURLBranch >= 0)
                {
                    entityLookupValue = "https://hub.docker.com/repository/docker/" + repoURL.Substring(0, idxRepoURLBranch);
                    repoURL = "https://hub.docker.com/repository/docker/" + repoURL.Substring(0, idxRepoURLBranch);

                    if (data.project.branch != "")
                    {
                        repoURL = repoURL + "/tree/" + data.project.branch;
                    }
                }
                else
                {
                    int idxRepoURLProject = repoURL.IndexOf(":");
                    entityLookupValue = "https://hub.docker.com/repository/docker/" + repoURL.Substring(0, idxRepoURLProject);
                    repoURL = "https://hub.docker.com/repository/docker/" + repoURL.Substring(0, idxRepoURLProject);
                }
            }
            else if (data.project.origin == "azure-repos")
            {
                //log.LogInformation("data: " + requestBody);
            }

            log.LogInformation(projectName + ", data.newIssues.Count: " + count);
            responseMessage = "No new issues found. Nothing to process!";

            name = name ?? data?.name;
            string browseUrl = data.project.browseUrl;
            int x = 0;

            // send data to New Relic
            var jsonArray = new JArray();

            if (data.newIssues.Count > 0)
            {
                log.LogInformation("New issues found!");
                for (int i = 0; i < data.newIssues.Count; i++)
                {
                    string id = data.newIssues[i].id.ToString();
                    string issueType = data.newIssues[i].issueType;
                    string pkgName = data.newIssues[i].pkgName;
                    int priorityScore = data.newIssues[i].priorityScore;
                    double cvssScore = data.newIssues[i].issueData.cvssScore;
                    string severity = data.newIssues[i].issueData.severity;
                    string descr = data.newIssues[i].issueData.description.ToString();
                    if (data.newIssues[i].issueData.description.ToString().Length >= 256)
                    {
                        descr = data.newIssues[i].issueData.description.ToString().Substring(0, 256);
                    }

                    var item = new
                    {
                        eventType = "SnykFindings",
                        entityType ="Repository",
                        projectName = projectName,
                        entityLookupValue = entityLookupValue,
                        issueInstanceKey = repoURL,
                        disclosureUrl = browseUrl,
                        imageId = data.project.imageId,
                        imageTag = data.project.imageTag,
                        imagePlatform = data.project.imagePlatform,
                        imageBaseImage = data.project.imageBaseImage,
                        containerImage = containerImage,
                        issueCountsBySeverityLow = data.project.issueCountsBySeverity.low,
                        issueCountsBySeverityHigh = data.project.issueCountsBySeverity.high,
                        issueCountsBySeverityMedium = data.project.issueCountsBySeverity.medium,
                        issueCountsBySeverityCritical = data.project.issueCountsBySeverity.critical,
                        snykOrigin = data.project.origin,
                        source = "Snyk", 
                        issueType = "Library Vulnerability",
                        snykIssueType = issueType,
                        pkgName = pkgName,
                        priorityScore = priorityScore,
                        severity = severity,
                        issueId = id,
                        message = descr
                    };

                    var jsonObj = JObject.FromObject(item);
                    jsonArray.Add(jsonObj);
                }
            }

            string json = jsonArray.ToString();

            string payload = json;
            //log.LogInformation("content: " + payload);

            var content = new StringContent(payload);

            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var NEW_RELIC_INSIGHTS_URL = Environment.GetEnvironmentVariable("NEW_RELIC_INSIGHTS_URL");
            var NEW_RELIC_INSIGHTS_INSERT_KEY = Environment.GetEnvironmentVariable("NEW_RELIC_INSIGHTS_INSERT_KEY");

            var url = NEW_RELIC_INSIGHTS_URL;
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("X-Insert-Key", NEW_RELIC_INSIGHTS_INSERT_KEY);
            var response = await client.PostAsync(url, content);

            string result = response.Content.ReadAsStringAsync().Result;
            log.LogInformation("response.StatusCode: " + response.StatusCode);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                x++;
            }
            //log.LogInformation("result: " + result);

            // write output as summary
            string output = "Successfully processed " + x + " issues.";
            log.LogInformation(output);
            responseMessage = output;
        }
        else
        {
            log.LogInformation("No project found!");
        }
    }
    catch (Exception ex)
    {
        log.LogInformation("ex: " + ex);
    }

    return new OkObjectResult(responseMessage);
}
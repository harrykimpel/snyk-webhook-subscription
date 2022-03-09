#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    log.LogInformation("C# HTTP trigger function processed a request.");

    string name = req.Query["name"];

    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    dynamic data = JsonConvert.DeserializeObject(requestBody);
    //log.LogInformation("data: " + requestBody);

    string responseMessage = "No valid payload received!";
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
        log.LogInformation(projectName + ", data.newIssues.Count: " + count);
        responseMessage = "No new issues found. Nothing to process!";


        name = name ?? data?.name;
        string browseUrl = data.project.browseUrl;
        int x = 0;

        // send data to New Relic
        StringBuilder sb = new StringBuilder();

        sb.Append("[{");
        sb.Append("  \"eventType\": \"SnykProject\",");
        sb.Append("  \"projectName\": \"" + projectName + "\",");
        sb.Append("  \"browseUrl\": \"" + browseUrl + "\",");
        sb.Append("  \"imageId\": \"" + data.project.imageId + "\",");
        sb.Append("  \"imageTag\": \"" + data.project.imageTag + "\",");
        sb.Append("  \"imagePlatform\": \"" + data.project.imagePlatform + "\",");
        sb.Append("  \"imageBaseImage\": \"" + data.project.imageBaseImage + "\",");
        sb.Append("  \"containerImage\": \"" + containerImage + "\",");
        sb.Append("  \"issueCountsBySeverityLow\": " + data.project.issueCountsBySeverity.low + ",");
        sb.Append("  \"issueCountsBySeverityHigh\": " + data.project.issueCountsBySeverity.high + ",");
        sb.Append("  \"issueCountsBySeverityMedium\": " + data.project.issueCountsBySeverity.medium + ",");
        sb.Append("  \"issueCountsBySeverityCritical\": " + data.project.issueCountsBySeverity.critical + "");
        sb.Append("}");

        if (data.newIssues.Count > 0)
        {
            log.LogInformation("New issues found!");
            for (int i = 0; i < data.newIssues.Count; i++)
            {
                //var item = (JObject)data.newIssues[i];
                //do something with item
                string id = data.newIssues[i].id.ToString();
                //log.LogInformation("data.newIssues[i].id:" + id);
                string descr = data.newIssues[i].issueData.description.ToString().Substring(0, 4096);
                //log.LogInformation("data.newIssues[i].issueData.description:" + descr);

                sb.Append(",{");
                sb.Append("  \"eventType\": \"SnykProjectIssue\",");
                sb.Append("  \"projectName\": \"" + projectName + "\",");
                sb.Append("  \"browseUrl\": \"" + browseUrl + "\",");
                sb.Append("  \"imageId\": \"" + data.project.imageId + "\",");
                sb.Append("  \"imageTag\": \"" + data.project.imageTag + "\",");
                sb.Append("  \"imagePlatform\": \"" + data.project.imagePlatform + "\",");
                sb.Append("  \"imageBaseImage\": \"" + data.project.imageBaseImage + "\",");
                sb.Append("  \"containerImage\": \"" + containerImage + "\",");
                sb.Append("  \"issueId\": \"" + id + "\",");
                sb.Append("  \"issueDescr\": \"" + descr + "\"");
                sb.Append("}");
            }
        }

        sb.Append("]");

        string payload = sb.ToString();
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

    return new OkObjectResult(responseMessage);
}
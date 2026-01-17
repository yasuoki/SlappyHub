namespace SlappyHub.WebServer;

/// <summary>
/// Represents the configuration options for an HTTP server.
/// </summary>
public class HttpServerOption
{
    public int Port { get; set; }
    public string StaticFilePath { get; set; }
    public string JsonText { get; set; }
    public string IndexFile { get; set; }
    public Dictionary<string,string> ContentTypes { get; set; }

    public HttpServerOption()
    {
        StaticFilePath = "";
        JsonText = "";
        IndexFile = "";
        ContentTypes = new Dictionary<string, string>();
        ContentTypes.Add(".css", "text/css");
        ContentTypes.Add(".html", "text/html");
        ContentTypes.Add(".js", "application/javascript");
        ContentTypes.Add(".json", "application/json");
        ContentTypes.Add(".png", "image/png");
        ContentTypes.Add(".jpg", "image/jpeg");
    }
}

using System.Net;
using System.Net.Sockets;
using System.Text;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

var filesDirectory = GetFilesFolder();

// Uncomment this block to pass the first stage
TcpListener server = new(IPAddress.Any, 4221);
server.Start();

while (true)
{
    var socket = server.AcceptSocket();
    Task.Run(() => HandleHttpRequest(socket, filesDirectory));
}

static string GetFilesFolder()
{
    var args = Environment.GetCommandLineArgs();

    string directory = string.Empty;
    for (int i = 1; i < args.Length; i++)
    {
        if (args[i].Equals("--directory"))
        {
            directory = i + 1 < args.Length ? args[i + 1] : string.Empty;
            break;
        }
    }

    return directory;
}

static async Task HandleHttpRequest(Socket socket, string filesDirectory)
{
    var requestBuilder = new StringBuilder();
    var buffer = new byte[1024];

    while (true)
    {
        var bytesRead = await socket.ReceiveAsync(buffer);

        if (bytesRead > 0)
        {
            requestBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
        }

        if (socket.Available == 0)
        {
            break;
        }
    }

    var requestContent = requestBuilder.ToString();

    var httpRequest = HttpRequestParser.Parse(requestContent);

    string? response;

    if (httpRequest.Target.Equals("/"))
    {
        response = "HTTP/1.1 200 OK\r\n\r\n";
    }
    else if (httpRequest.Target.StartsWith("/echo/"))
    {
        var echoContent = httpRequest.Target[6..];
        response = $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length:{echoContent.Length}\r\n\r\n{echoContent}";
    }
    else if (httpRequest.Target.Equals("/user-agent"))
    {
        var userAgent = httpRequest.Headers.GetValueOrDefault("User-Agent", string.Empty);
        response = $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length:{userAgent.Length}\r\n\r\n{userAgent}";
    }
    else if (httpRequest.Target.StartsWith("/files/"))
    {
        var fileName = httpRequest.Target[7..];
        var path = Path.Combine(filesDirectory, fileName);
        response = httpRequest.Method.Equals("GET") ? HandleGetFile(path) : HandlePostFile(path, httpRequest.Body);
    }
    else
    {
        response = "HTTP/1.1 404 Not Found\r\n\r\n";
    }

    await socket.SendAsync(Encoding.UTF8.GetBytes(response));

    static string HandleGetFile(string path)
    {
        string? response;
        if (File.Exists(path))
        {
            var fileContent = File.ReadAllText(path);
            var contentLength = Encoding.UTF8.GetByteCount(fileContent);
            response = $"HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nContent-Length:{contentLength}\r\n\r\n{fileContent}";
        }
        else
        {
            response = "HTTP/1.1 404 Not Found\r\n\r\n";
        }

        return response;
    }

    static string HandlePostFile(string path, string fileContent)
    {
        File.WriteAllText(path, fileContent);

        return "HTTP/1.1 201 Created\r\n\r\n";
    }
}

public class HttpRequestParser
{
    private static readonly char[] HeaderSeparator = new char[] { ':' };
    private static readonly char[] RequestLineSeparator = new char[] { ' ' };

    public static HttpRequest Parse(string requestContent)
    {
        var lines = requestContent.Split("\r\n", StringSplitOptions.TrimEntries);

        var (method, target, version) = ParseRequestLine(lines[0]);
        var (headers, body) = ParseHeadersAndBody(lines);

        return new HttpRequest
        {
            Method = method,
            Target = target,
            Version = version,
            Headers = headers,
            Body = body
        };
    }

    private static (string Method, string Target, string Version) ParseRequestLine(string requestLine)
    {
        string[] tokens = requestLine.Split(
            RequestLineSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (tokens[0], tokens[1], tokens[2]);
    }

    private static (Dictionary<string, string> Headers, string Body) ParseHeadersAndBody(string[] lines)
    {
        var headers = new Dictionary<string, string>();
        var body = string.Empty;

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrEmpty(line))
            {
                if (i + 1 < lines.Length)
                {
                    body = lines[i + 1];
                }
                break;
            }

            var tokens = line.Split(
                HeaderSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (tokens.Length == 2)
            {
                headers.Add(tokens[0], tokens[1]);
            }
        }

        return (headers, body);
    }
}

public class HttpRequest
{
    public string Method { get; set; }
    public string Target { get; set; }
    public string Version { get; set; }

    public Dictionary<string, string> Headers { get; set; }

    public string Body { get; set; }
}
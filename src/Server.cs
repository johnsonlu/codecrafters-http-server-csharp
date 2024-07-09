using System.Net;
using System.Net.Sockets;
using System.Text;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

// Uncomment this block to pass the first stage
TcpListener server = new(IPAddress.Any, 4221);
server.Start();

var socket = server.AcceptSocket();

var buffer = new byte[1024];
var bytesRead = await socket.ReceiveAsync(buffer);

var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

var (_, target, _) = ParseRequestLine(request);

string? response;

if (target.Equals("/"))
{
    response = "HTTP/1.1 200 OK\r\n\r\n";
}
else if (target.StartsWith("/echo/"))
{
    var echoContent = target[6..];
    response = $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length:{echoContent.Length}\r\n\r\n{echoContent}";
}
else
{
    response = "HTTP/1.1 404 Not Found\r\n\r\n";
}

await socket.SendAsync(Encoding.UTF8.GetBytes(response));

static (string Method, string Target, string Version) ParseRequestLine(string httpRequest)
{
    var firstLineIndex = httpRequest.IndexOf("\r\n");
    var requestLine = httpRequest[..(firstLineIndex + 1)];

    string[] tokens = requestLine.Split(
        new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    return (tokens[0], tokens[1], tokens[2]);
}
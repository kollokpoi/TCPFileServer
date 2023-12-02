using System.Net.Sockets;
using System.Net;
using System.Text;
using System.IO;

IPAddress localAddress = IPAddress.Loopback;
const int localPort = 7777;
const string basePath = "H:\\SharedFolder";

var server = new TcpListener(localAddress, localPort);
server.Start();

while (true)
{
    var client = await server.AcceptTcpClientAsync();
    _ = Task.Run(() => ClientHandler(client));
}

return;

async Task ClientHandler(TcpClient client)
{
    var stream = client.GetStream();
    using var streamWriter = new StreamWriter(stream, Encoding.UTF8);
    using var streamReader = new StreamReader(stream, Encoding.UTF8);
    using var _ = client;

    while (client.Connected)
    {
        var command = streamReader.ReadLine();

        if (command!=null)
        {
            var commandParts = command.Split(' ');

            if (commandParts[0].StartsWith("download"))
            {
                var name = commandParts[1];
                string path = Path.Combine(basePath, name);
                path = path.Replace("\0", "");
                await SendFile(path, stream);
            }
            else if(commandParts[0].StartsWith("send"))
            {
                var fileName = commandParts[1].Split('\\')[^1];
                var buf = new byte[65536];
                await ReadBytes(sizeof(long), buf,stream);
                var remainingLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt64(buf, 0));

                var filePath = Path.Combine(basePath, fileName);
                await using var file = File.Create(filePath);

                while (remainingLength > 0)
                {
                    var lengthToRead = (int)Math.Min(remainingLength, buf.Length);
                    await ReadBytes(lengthToRead, buf, stream);
                    await file.WriteAsync(buf, 0, lengthToRead);
                    remainingLength -= lengthToRead;
                }
            }
            else if (commandParts[0].StartsWith("list"))
            {
                string[] files = Directory.GetFiles(basePath);
                foreach (var file in files)
                {
                    streamWriter.WriteLine(Path.GetFileName(file));
                }
                streamWriter.WriteLine("200");
                streamWriter.Flush();
            }
        }
    }



}
static async Task SendFile(string filePath, NetworkStream stream)
{
    await using var file = File.OpenRead(filePath);
    var length = file.Length;
    var lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(length));
    await stream.WriteAsync(lengthBytes);
    await file.CopyToAsync(stream);
}
async Task ReadBytes(int howmuch, byte[] buf, NetworkStream stream)
{
    int readPos = 0;
    while (readPos < howmuch)
    {
        var actuallyRead = await stream.ReadAsync(buf, readPos, howmuch - readPos);
        if (actuallyRead == 0)
            throw new EndOfStreamException();
        readPos += actuallyRead;
    }
}
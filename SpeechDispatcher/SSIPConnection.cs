/*
* Copyright (C) 2022 Rastislav Kish
*
* This program is free software: you can redistribute it and/or modify
* it under the terms of the GNU Lesser General Public License as published by
* the Free Software Foundation, version 2.1.
*
* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Lesser General Public License for more details.
*
* You should have received a copy of the GNU Lesser General Public License
* along with this program. If not, see <https://www.gnu.org/licenses/>.
*/

using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SpeechDispatcher;

class SSIPConnection {

    public CallbackHandler? Callback {get; set;}=null;

    Socket socket;
    StreamWriter? writer=null;

    ChannelReader<ServerMessage> communicationChannelReader; //Supposed for single reader
    object sendInputLock=new object();

    Task communicationTask;

    public SSIPConnection(CommunicationMethod communicationMethod)
        {
        try {
            switch (communicationMethod) {
                case UnixSocketCommunicationMethod u: {
                    var endPoint=new UnixDomainSocketEndPoint(u.SocketPath);
                    socket=new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Unspecified);
                    socket.Connect(endPoint);
                    break;
                    }
                case InetSocketCommunicationMethod i: {
                    var endPoint=new IPEndPoint(i.Host, i.Port);
                    socket=new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    socket.Connect(endPoint);
                    break;
                    }
                default:
                throw new ArgumentException("Passed communication method is not supported.");
                }
            }
        catch (Exception e) {
            throw new SSIPCommunicationException($"Can't open socket using method {communicationMethod}.", e);
            }

        var communicationChannel=Channel.CreateUnbounded<ServerMessage>(new UnboundedChannelOptions {
            SingleReader=true,
            SingleWriter=true,
            });
        communicationChannelReader=communicationChannel.Reader;

        communicationTask=Task.Run(() => Communication(communicationChannel.Writer));

        // Wait until writer gets assigned from the communication thread
        while (writer is null && !communicationTask.IsCompleted)
        Task.Delay(5).Wait();
        }

    public ServerMessage SendCommand(string command, params object[] args)
        {
        lock (sendInputLock)
            {
            var cmd=string.Join(" ", command, string.Join(" ", args))+NEWLINE;

            try {
                writer?.Write(cmd);
                writer?.Flush();
                }
            catch (Exception e) {
                throw new SSIPCommunicationException("Speech Dispatcher connection lost.", e);
                }

            var readTask=communicationChannelReader.ReadAsync().AsTask();
            readTask.Wait();
            var response=readTask.Result;

            if (response is null)
            throw new SSIPCommunicationException();

            if (response.Code/100!=2)
            throw new SSIPCommandException(response.Code, response.Message, cmd);

            return response;
            }
        }
    public ServerMessage SendData(string data)
        {
        lock (sendInputLock)
            {
            data=data.Replace("\r", "");
            data=dotRegex.Replace(data, "..");
            if (!data.EndsWith('\n'))
            data+='\n';
            data=data.Replace("\n", NEWLINE);

            data+=$".{NEWLINE}";

            try {
                writer?.Write(data);
                writer?.Flush();
                }
            catch (Exception e) {
                throw new SSIPCommunicationException("Speech Dispatcher connection lost.", e);
                }

            var readTask=communicationChannelReader.ReadAsync().AsTask();
            readTask.Wait();
            var response=readTask.Result;

            if (response is null)
            throw new SSIPCommunicationException();

            if (response.Code/100!=2)
            throw new SSIPCommandException(response.Code, response.Message, data);

            return response;
            }
        }

    public void Close()
        {
        writer=null;
        socket.Shutdown(SocketShutdown.Both);
        socket.Close();
        communicationTask.Wait();
        }

    void Communication(ChannelWriter<ServerMessage> communicationChannelWriter)
        {
        using var networkStream=new NetworkStream(socket);
        using var reader=new StreamReader(networkStream);
        using var writer=new StreamWriter(networkStream);

        this.writer=writer;

        while (true)
            {
            var serverMessage=ReceiveMessage(reader);

            if (serverMessage is null)
            return;

            var (code, message, data)=serverMessage;

            if (code/100!=7) {
                communicationChannelWriter.TryWrite(serverMessage);
                continue;
                }

            if (Callback is not null) {
                CallbackType callbackType=MapCallbackType(code);
                string? indexMark=(callbackType==CallbackType.IndexMark) ? data[2]: null;

                int messageId=int.Parse(data[0]);
                int clientId=int.Parse(data[1]);

                Callback?.Invoke(messageId, clientId, callbackType, indexMark);
                }
            }
        }
    ServerMessage? ReceiveMessage(StreamReader reader)
        {
        var data=new List<string>();

        while (true)
            {
            string? line=reader.ReadLine();

            if (line==null)
            return null;

            if (line.Length<4)
            throw new Exception("Invalid data received from the server.");

            string code=line.Substring(0, 3);
            char separator=line[3];
            string text=line.Substring(4);

            if (!IsAlphanumeric(code) || separator is not (' ' or '-'))
            throw new Exception("Invalid data received from the server.");

            if (separator==' ')
            return new ServerMessage(int.Parse(code), text, data);

            data.Add(text);
            }
        }

    const string NEWLINE="\r\n";
    static Regex dotRegex=new Regex(@"^\.$", RegexOptions.Multiline);

    static CallbackType MapCallbackType(int code)
    => code switch {
        700 => CallbackType.IndexMark,
        701 => CallbackType.Begin,
        702 => CallbackType.End,
        703 => CallbackType.Cancel,
        704 => CallbackType.Pause,
        705 => CallbackType.Resume,
        _ => throw new ArgumentException($"{code} is not a callback type code."),
        };

    static bool IsAlphanumeric(string text)
        {
        foreach (char ch in text) {
            if (ch is not ((>='a' and <= 'z') or (>='A' and <='Z') or (>='0' and <='9')))
            return false;
            }

        return true;
        }

    }

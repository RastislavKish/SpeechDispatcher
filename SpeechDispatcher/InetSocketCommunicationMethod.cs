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

namespace SpeechDispatcher;

public record InetSocketCommunicationMethod(IPAddress Host, int Port): CommunicationMethod {

    public InetSocketCommunicationMethod(): this(defaultHost, defaultPort)
        {

        }
    public InetSocketCommunicationMethod(string host, int port): this(ResolveHost(host), port)
        {

        }
    public InetSocketCommunicationMethod(string host): this(ResolveHost(host), defaultPort)
        {

        }
    public InetSocketCommunicationMethod(int port): this(defaultHost, port)
        {

        }

    public override bool CanAutospawn(out string? reason)
        {
        if (!IPAddress.IsLoopback(Host)) {
            reason=$"Unable to autospawn server on remote host {Host}. Choose a different address, or start the server manually.";
            return false;
            }

        reason=null;
        return true;
        }

    readonly static IPAddress defaultHost=IPAddress.Loopback;
    readonly static int defaultPort=6560;

    static IPAddress ResolveHost(string host)
    => Dns.GetHostAddresses(host).First();

    }

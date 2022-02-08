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

namespace SpeechDispatcher;

public record UnixSocketCommunicationMethod(string SocketPath): CommunicationMethod {

    public UnixSocketCommunicationMethod(): this(GetDefaultSocketPath())
        {

        }

    public override bool CanAutospawn(out string? reason)
        {
        reason=null;
        return true;
        }

    static string GetDefaultSocketPath()
        {
        string runtimeDirectory=Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")
        ?? Environment.GetEnvironmentVariable("XDG_CACHE_HOME")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");

        string socketPath=Path.Combine(runtimeDirectory, "speech-dispatcher", "speechd.sock");

        return socketPath;
        }

    }

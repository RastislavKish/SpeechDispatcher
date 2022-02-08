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

using System.Collections.Generic;

namespace SpeechDispatcher;

class CallbackHandler {

    int clientId;
    Dictionary<int, (Action<CallbackType, string?> callback, CallbackTypes eventTypes)> callbacks=new();
    object callbacksLock=new();

    public CallbackHandler(int clientId)
        {
        this.clientId=clientId;
        }

    public void Invoke(int messageId, int clientId, CallbackType callbackType, string? indexMark)
        {
        if (clientId!=this.clientId)
        return;

        lock (callbacksLock)
            {
            if (callbacks.TryGetValue(messageId, out var value)) {
                var (callback, eventTypes)=value;

                if (eventTypes.HasFlag(callbackType))
                callback(callbackType, indexMark);

                if (callbackType==CallbackType.End || callbackType==CallbackType.Cancel)
                callbacks.Remove(messageId);
                }
            }
        }
    public void AddCallback(int messageId, Action<CallbackType, string?> callback, CallbackTypes eventTypes)
        {
        lock (callbacksLock)
            {
            callbacks[messageId]=(callback, eventTypes);
            }
        }

    }

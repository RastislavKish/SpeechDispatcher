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

using System.Diagnostics;

namespace SpeechDispatcher;

public class SSIPClient: IDisposable {

    SSIPConnection connection;
    CallbackHandler callbackHandler;

    public SSIPClient(string name, string component="default", string user="unknown", CommunicationMethod? communicationMethod=null, bool autospawn=true)
        {
        communicationMethod??=new UnixSocketCommunicationMethod();

        connection=ConnectWithAutospawn(communicationMethod, autospawn);
        callbackHandler=InitializeConnection(user, name, component);
        }

    SSIPConnection ConnectWithAutospawn(CommunicationMethod communicationMethod, bool autospawn)
        {
        try {
            return new SSIPConnection(communicationMethod);
            }
        catch (SSIPCommunicationException communicationException) {
            if (autospawn) {
                try {
                    ServerSpawn(communicationMethod);
                    }
                catch (SpawnException spawnException) {
                    communicationException=new SSIPCommunicationException(communicationException.Message, spawnException);

                    throw communicationException;
                    }

                return new SSIPConnection(communicationMethod);
                }
            else
            throw communicationException;
            }
        }
    void ServerSpawn(CommunicationMethod communicationMethod)
        {
        string serverPath=Environment.GetEnvironmentVariable("SPEECHD_CMD")
        ?? "speech-dispatcher";

        if (!communicationMethod.CanAutospawn(out string? reason))
        throw new SpawnException(reason ?? "");

        string[] args=communicationMethod switch {
            UnixSocketCommunicationMethod u => new string[] {"--spawn", "--socket-path", u.SocketPath},
            InetSocketCommunicationMethod i => new string[] {"--spawn", "--host", i.Host.ToString(), "--port", i.Port.ToString()},
            _ => throw new ArgumentException("Passed communication method is not supported."),
            };

        var startInfo=new ProcessStartInfo(serverPath) {
            RedirectStandardError=true,
            };

        foreach (string arg in args)
        startInfo.ArgumentList.Add(arg);

        Process server=Process.Start(startInfo)
        ?? throw new SpawnException($"Unable to start server from path {serverPath}.");

        server.WaitForExit();

        if (server.ExitCode!=0)
        throw new SpawnException($"Server refused to autospawn. Reason: {server.StandardError.ReadToEnd()}");
        }

    CallbackHandler InitializeConnection(string user, string name, string component)
        {
        string fullName=$"{user}:{name}:{component}";
        connection.SendCommand("SET", Scope.SELF, "CLIENT_NAME", fullName);
        var (code, message, data)=connection.SendCommand("HISTORY", "GET", "CLIENT_ID");
        int clientId=int.Parse(data[0]);

        var callbackHandler=new CallbackHandler(clientId);
        connection.Callback=callbackHandler;

        TurnOnEvent(CallbackType.IndexMark);
        TurnOnEvent(CallbackType.Begin);
        TurnOnEvent(CallbackType.End);
        TurnOnEvent(CallbackType.Cancel);
        TurnOnEvent(CallbackType.Pause);
        TurnOnEvent(CallbackType.Resume);

        return callbackHandler;

        void TurnOnEvent(CallbackType evt)
        => connection.SendCommand("SET", Scope.SELF, "NOTIFICATION", CallbackTypeToString(evt), "on");
        }

    public void SetPriority(Priority priority)
    => connection.SendCommand("SET", Scope.SELF, "PRIORITY", PriorityToString(priority));

    public void SetDataMode(DataMode dataMode)
    => connection.SendCommand("SET", Scope.SELF, "SSML_MODE", (dataMode==DataMode.SSML) ? 1: 0);

    public ServerMessage Speak(string text, Action<CallbackType, string?>? callback=null, CallbackTypes eventTypes=CallbackTypes.All)
        {
        connection.SendCommand("SPEAK");
        ServerMessage result=connection.SendData(text);

        if (callback is not null) {
            int messageId=int.Parse(result.Data[0]);
            callbackHandler.AddCallback(messageId, callback, eventTypes);
            }

        return result;
        }

    public void Char(char ch)
    => connection.SendCommand("CHAR", (ch!=' ') ? ch: "space");

    public void Key(string key)
    => connection.SendCommand("KEY", key);

    public void SoundIcon(string soundIcon)
    => connection.SendCommand("SOUND_ICON", soundIcon);

    public void Cancel(string scope=Scope.SELF)
    => connection.SendCommand("CANCEL", scope);

    public void Stop(string scope=Scope.SELF)
    => connection.SendCommand("STOP", scope);

    public void Pause(string scope=Scope.SELF)
    => connection.SendCommand("PAUSE", scope);

    public void Resume(string scope=Scope.SELF)
    => connection.SendCommand("RESUME", scope);

    public List<string> ListOutputModules()
        {
        var (_, _, data)=connection.SendCommand("LIST", "OUTPUT_MODULES");
        return data;
        }

    public List<VoiceInfo> ListSynthesisVoices()
        {
        List<string> data;

        try {
            (_, _, data)=connection.SendCommand("LIST", "SYNTHESIS_VOICES");
            }
        catch (SSIPCommandException) {
            return new List<VoiceInfo>();
            }

        var result=new List<VoiceInfo>();
        foreach (string line in data) {
            var components=line.Split("\t").ToList();
            string name=components[0];
            string? language=(components.Count>1) ? components[1]: null;
            string? variant=(components.Count>2) ? components[2]: null;

            var voiceInfo=new VoiceInfo(name, language, variant);

            result.Add(voiceInfo);
            }

        return result;
        }

    public void SetLanguage(string language, string scope=Scope.SELF)
    => connection.SendCommand("SET", scope, "LANGUAGE", language);

    public string? GetLanguage()
        {
        var (_, _, data)=connection.SendCommand("GET", "LANGUAGE");

        if (data.Count>0)
        return data[0];

        return null;
        }

    public void SetOutputModule(string name, string scope=Scope.SELF)
    => connection.SendCommand("SET", scope, "OUTPUT_MODULE", name);

    public string? GetOutputModule()
        {
        var (_, _, data)=connection.SendCommand("GET", "OUTPUT_MODULE");

        if (data.Count>0)
        return data[0];

        return null;
        }

    public void SetPitch(int pitch, string scope=Scope.SELF)
        {
        if (pitch is not (>=-100 and <=100))
        throw new ArgumentException($"Invalid value for pitch ({pitch}). The valid range is -100 to 100.");

        connection.SendCommand("SET", scope, "PITCH", pitch);
        }
    public int? GetPitch()
        {
        var (_, _, data)=connection.SendCommand("GET", "PITCH");

        if (data.Count>0)
        return int.Parse(data[0]);

        return null;
        }

    public void SetPitchRange(int pitchRange, string scope=Scope.SELF)
        {
        if (pitchRange is not (>=-100 and <=100))
        throw new ArgumentException($"Invalid value for pitch range ({pitchRange}). The valid range is -100 to 100.");

        connection.SendCommand("SET", scope, "PITCH_RANGE", pitchRange);
        }
    public int? GetPitchRange()
        {
        var (_, _, data)=connection.SendCommand("GET", "PITCH_RANGE");

        if (data.Count>0)
        return int.Parse(data[0]);

        return null;
        }

    public void SetRate(int rate, string scope=Scope.SELF)
        {
        if (rate is not (>=-100 and <=100))
        throw new ArgumentException($"Invalid value for rate ({rate}). The valid range is -100 to 100.");

        connection.SendCommand("SET", scope, "RATE", rate);
        }
    public int? GetRate()
        {
        var (_, _, data)=connection.SendCommand("GET", "RATE");

        if (data.Count>0)
        return int.Parse(data[0]);

        return null;
        }

    public void SetVolume(int volume, string scope=Scope.SELF)
        {
        if (volume is not (>=-100 and <=100))
        throw new ArgumentException($"Invalid value for volume ({volume}). The valid range is -100 to 100.");

        connection.SendCommand("SET", scope, "VOLUME", volume);
        }
    public int? GetVolume()
        {
        var (_, _, data)=connection.SendCommand("GET", "VOLUME");

        if (data.Count>0)
        return int.Parse(data[0]);

        return null;
        }

    public void SetPunctuation(PunctuationMode punctuationMode, string scope=Scope.SELF)
    => connection.SendCommand("SET", scope, "PUNCTUATION", PunctuationModeToString(punctuationMode));

    public PunctuationMode? GetPunctuation()
        {
        var (_, _, data)=connection.SendCommand("GET", "PUNCTUATION");

        if (data.Count>0)
        return PunctuationModeParse(data[0]);

        return null;
        }

    public void SetSpelling(bool spelling, string scope=Scope.SELF)
    => connection.SendCommand("SET", scope, "SPELLING", (spelling) ? "on": "off");

    public void SetCapitalLetterRecognition(CapitalLetterRecognitionMode capitalLetterRecognitionMode, string scope=Scope.SELF)
    => connection.SendCommand("SET", scope, "CAP_LET_RECOGN", CapitalLetterRecognitionModeToString(capitalLetterRecognitionMode));

    public void SetVoice(VoiceType voiceType, string scope=Scope.SELF)
    => connection.SendCommand("SET", scope, "VOICE_TYPE", VoiceTypeToString(voiceType));

    public void SetSynthesisVoice(string name, string scope=Scope.SELF)
    => connection.SendCommand("SET", scope, "SYNTHESIS_VOICE", name);

    public void SetPauseContext(int pauseContext, string scope=Scope.SELF)
    => connection.SendCommand("SET", scope, "PAUSE_CONTEXT", pauseContext);

    public void SetDebug(bool debug)
    => connection.SendCommand("SET", Scope.ALL, "DEBUG", (debug) ? "on": "off");

    public void SetDebugDestination(string debugDestination)
    => connection.SendCommand("SET", Scope.ALL, "DEBUG_DESTINATION", debugDestination);

    public void BlockBegin()
    => connection.SendCommand("BLOCK", "BEGIN");

    public void BlockEnd()
    => connection.SendCommand("BLOCK", "END");

    public void Close()
        {
        connection.Close();
        }

    public void Dispose()
    => Close();

    //Helper methods

    static string CallbackTypeToString(CallbackType callbackType)
    => callbackType switch {
        CallbackType.IndexMark => "index_marks",
        CallbackType.Begin => "begin",
        CallbackType.End => "end",
        CallbackType.Cancel => "cancel",
        CallbackType.Pause => "pause",
        CallbackType.Resume => "resume",
        _ => throw new ArgumentException("CallbackType to string conversion received an unknown enum value."),
        };

    static string PriorityToString(Priority priority)
    => priority switch {
        Priority.Important => "important",
        Priority.Text => "text",
        Priority.Message => "message",
        Priority.Notification => "notification",
        Priority.Progress => "progress",
        _ => throw new ArgumentException("Priority to string conversion received an unknown enum value."),
        };

    static string PunctuationModeToString(PunctuationMode punctuationMode)
    => punctuationMode switch {
        PunctuationMode.All => "all",
        PunctuationMode.None => "none",
        PunctuationMode.Some => "some",
        PunctuationMode.Most => "most",
        _ => throw new ArgumentException("PunctuationMode to string conversion received an unknown enum value."),
        };

    static PunctuationMode PunctuationModeParse(string input)
    => input switch {
        "all" => PunctuationMode.All,
        "none" => PunctuationMode.None,
        "some" => PunctuationMode.Some,
        "most" => PunctuationMode.Most,
        _ => throw new ArgumentException($"\"{input}\" is not a valid PunctuationMode representation."),
        };

    static string CapitalLetterRecognitionModeToString(CapitalLetterRecognitionMode capitalLetterRecognitionMode)
    => capitalLetterRecognitionMode switch {
        CapitalLetterRecognitionMode.None => "none",
        CapitalLetterRecognitionMode.Spell => "spell",
        CapitalLetterRecognitionMode.Icon => "icon",
        _ => throw new ArgumentException("CapitalLetterRecognitionMode to string conversion received an unknown enum value."),
        };

    static string VoiceTypeToString(VoiceType voiceType)
    => voiceType switch {
        VoiceType.Female1 => "female1",
        VoiceType.Female2 => "female2",
        VoiceType.Female3 => "female3",
        VoiceType.Male1 => "male1",
        VoiceType.Male2 => "male2",
        VoiceType.Male3 => "male3",
        VoiceType.ChildFemale1 => "child_female1",
        VoiceType.ChildFemale2 => "child_female2",
        VoiceType.ChildFemale3 => "child_female3",
        VoiceType.ChildMale1 => "child_male1",
        VoiceType.ChildMale2 => "child_male2",
        VoiceType.ChildMale3 => "child_male3",
        _ => throw new ArgumentException("VoiceType to string conversion received an unknown enum value."),
        };

    }

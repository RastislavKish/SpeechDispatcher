# SpeechDispatcher

A .NET library for communication with SpeechDispatcher.

## DISCLAIMER

This library at the time of the writing fully implements the functionality offered by the official Python speechd client.
However, in order to deliver the best usage experience for development primarily in C#, testing on real projects is necessary to find the most efficient and comfortable solutions.
Therefore, while the project is in its 0.x versions, compatibility breaking changes can occur.

## Usage

First, add the SpeechDispatcher package to your dependencies:

```
$ dotnet add package SpeechDispatcher
```

The API closely follows the one of Python speechd:

```
using SpeechDispatcher;

using var client=new SSIPClient("ExampleApp");
client.Speak("Hello!");
Console.ReadKey();
```

Don't forget to dispose the SSIPClient instance when you no longer need it, either by the using construction as shown above, calling directly its Dispose method or its Close method (all mentioned approaches do the same task).

## A note on the Scope class

Most of the classes that represent a choice in this library are enums, like Priority, PunctuationMode, DataMode or VoiceType.
An exception to this approach is the Scope class. Since the user may want to enter a numbered scope besides the self and all variants, the scope parameter of various functions has string type.
Scope class contains constants representing the recognized variants - SELF and ALL, while a specific scope can be used manually when necessary.

## Acknowledgements

This library is from large part a C# rewrite of the Python speechd library. While a significant refactoring was done to modernize the code, make use of the latest C# features and overal fit its philosophy and conventions, the work was significantly easier with already created and tested algorithms.

I'd therefore like to thank speechd authors for their awesome job and a foolproof documentation, which made the development very easy and straight-forward.

## License

Copyright (C) 2022 Rastislav Kish

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
the Free Software Foundation, version 2.1.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public License
along with this program. If not, see <https://www.gnu.org/licenses/>.


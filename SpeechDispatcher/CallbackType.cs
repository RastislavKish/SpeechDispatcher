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

//The enum variants values have to correspond to CallbackTypes values in order to make it possible to use them in flag comparison
public enum CallbackType {
    IndexMark=1,
    Begin=2,
    End=4,
    Cancel=8,
    Pause=16,
    Resume=32,
    }

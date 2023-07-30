using Godot;
using System;

public partial class BoardTheme : Resource
{
    // board colors

    [Export]
    public Color LightColor;
    [Export]
    public Color DarkColor;

    // last move color

    [Export]
    public Color LastMoveColor;
}

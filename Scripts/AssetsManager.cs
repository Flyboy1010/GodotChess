using Godot;
using System;

public static class AssetsManager
{
    // pieces atlas

    public static readonly Texture2D ClassicPiecesTextureAtlas = GD.Load<Texture2D>("res://Assets/Sprites/classicpieces2.png");
    public static readonly Texture2D NeoPiecesTextureAtlas = GD.Load<Texture2D>("res://Assets/Sprites/neopieces.png");
    public static readonly Texture2D GlassPiecesTextureAtlas = GD.Load<Texture2D>("res://Assets/Sprites/glasspieces.png");
    public static readonly Texture2D ClassicOldPiecesTextureAtlas = GD.Load<Texture2D>("res://Assets/Sprites/pieces.png");

    // board themes

    public static readonly BoardTheme PurpleBoardTheme = GD.Load<BoardTheme>("res://Assets/Themes/PurpleTheme.tres");
    public static readonly BoardTheme BrownBoardTheme = GD.Load<BoardTheme>("res://Assets/Themes/BrownTheme.tres");
    public static readonly BoardTheme BlueBoardTheme = GD.Load<BoardTheme>("res://Assets/Themes/BlueTheme.tres");

    // materials

    public static readonly Material CircleHoleMaterial = GD.Load<Material>("res://Assets/Shaders&Materials/CircleHoleMaterial.tres");
    public static readonly Material CircleMaterial = GD.Load<Material>("res://Assets/Shaders&Materials/CircleMaterial.tres");
}

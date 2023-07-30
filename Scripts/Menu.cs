using Godot;
using System;
using static Board;

public partial class Menu : Control
{
    [Export]
    private ChessGame game;

    [Export]
    private SettingsMenu settingsMenu;

    private void _OnPlayWhiteButtonPressed()
    {
        game.PlayAsColor(StartFEN, Piece.Color.White);
    }

    private void _OnPlayBlackButtonPressed()
    {
        game.PlayAsColor(StartFEN, Piece.Color.Black);
    }

    private void _OnSettingsButtonPressed()
    {
        settingsMenu.OpenMenu();
    }

    private void _OnQuitButtonPressed()
    {
        GetTree().Root.PropagateNotification((int)NotificationWMCloseRequest); // propagate quit notification
        GetTree().Quit(); // quit the game
    }
}

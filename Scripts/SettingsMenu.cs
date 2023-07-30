using Godot;
using System;
using static Board;

public partial class SettingsMenu : Control
{
	[Export]
	private ChessGame game;

    [Export]
    private LineEdit fenString;

    [Export]
	private Slider eloSlider;

	[Export]
	private Label eloValueLabel;

	// close menu and & sub menus

	public void CloseMenu()
	{
		Visible = false;
		game.SetProcess(true);
	}

	public void OpenMenu()
	{
		Visible = true;
		game.SetProcess(false);
	}

	private void _OnPromotionPiecesOptionButtonItemSelected(int index)
	{
		Piece.Type type = (Piece.Type)(index + 2);
		game.SelectPromotionPieceType(type);

		GD.Print("Promotion piece changed to: ", type);
	}

	private void _OnFlipBoardButtonPressed()
	{
		game.FlipBoard();
	}

	private void _OnFullscreenButtonPressed()
	{
		DisplayServer.WindowMode windowMode = DisplayServer.WindowGetMode();

		switch (windowMode)
		{
			case DisplayServer.WindowMode.Maximized:
			case DisplayServer.WindowMode.Windowed:
				DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
				break;
			default:
				DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
				break;
		}
    }

    private void _OnGetFENButtonPressed()
	{
		string fen = game.GetFEN();

        DisplayServer.ClipboardSet(fen);
        GD.Print("Current fen position: ", fen);
    }

	private void _OnPlayWhiteButtonPressed()
	{
		string fen = fenString.Text;
		game.PlayAsColor(fen, Piece.Color.White);
	}

    private void _OnPlayBlackButtonPressed()
    {
        string fen = fenString.Text;
        game.PlayAsColor(fen, Piece.Color.Black);
    }

    private void _OnComputerEloSliderDragEnded(bool valueChanged)
	{
		if (valueChanged)
		{
			int elo = (int)eloSlider.Value;

			if (elo >= 2900)
			{
				elo = int.MaxValue;
			}

            game.SelectComputerELO(elo);

			GD.Print("Computer ELO changed to: ", elo);
		}
	}

	private void _OnComputerEloSliderValueChanged(int value)
	{
		eloValueLabel.Text = string.Format("{0}", value < 2900 ? value : "Max");
	}

	private void _OnBackButtonPressed()
	{
		CloseMenu();
    }

    public override void _UnhandledInput(InputEvent e)
    {
		// pressing esc is the same as pressing the back button

		if (Input.IsActionJustPressed("ui_cancel"))
		{
			CloseMenu();
        }
    }
}

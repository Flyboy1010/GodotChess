using Godot;
using System;
using System.Collections.Generic;
using System.Threading;
using static Board;

public partial class ChessGame : Node
{
    private enum GameState
    {
        AnimateMove,
        WaitingForAnimationToFinish,
        AnimationJustFinished,
        WaitingForComputer,
        PlayerMoving,
        GameEnd
    }

    private struct PieceSelection
    {
        public bool isSelected;
        public bool isHolding;
        public int squareIndex;
        public List<Move> legalMoves;
    }

    private struct MoveAnimation
    {
        public Move move;
        public bool dragged;
    }

    private static readonly string checkmateString = "Checkmate!";
    private static readonly string drawString = "Draw!";

    // theme

    [Export]
    private Texture2D piecesTextureAtlas;
    [Export]
    private BoardTheme boardTheme;

    [Export]
    private int computerElo = 1700;

    [Export]
    private Label gameEndLabel; // shows why the game ended (checkmate or stalemate)

    private Board board = new Board();
    private BoardGraphics boardGraphics;
    private Dictionary<string, AudioStreamPlayer> sounds = new Dictionary<string, AudioStreamPlayer>();

    private PieceSelection pieceSelection;
    private MoveAnimation moveAnimation;
    private Piece.Color playerColor = Piece.Color.White;
    private GameState gameState = GameState.PlayerMoving;

    // computer

    private bool engineRunning = true;
    private EngineConnector engineConnector = new EngineConnector();
    private Thread computerThread;
    private System.Threading.Semaphore computerThreadBarrier = new System.Threading.Semaphore(0, 1);
    private System.Threading.Mutex mutex = new System.Threading.Mutex();
    private System.Threading.Mutex engineConnectorMutex = new System.Threading.Mutex();

    // select & deselect piece

    private void SelectPiece(int squareIndex, List<Move> legalMoves)
    {
        pieceSelection.isSelected = true;
        pieceSelection.isHolding = true;
        pieceSelection.squareIndex = squareIndex;
        pieceSelection.legalMoves = legalMoves;
    }

    private void DeselectPiece()
    {
        pieceSelection.isSelected = false;
        pieceSelection.isHolding = false;
    }

    // plays as selected color with the selected fen

    public void PlayAsColor(string fen, Piece.Color color)
    {
        // validate fen string

        if (!FenValidator.IsFenStringValid(fen))
        {
            GD.Print(fen, " is not a valid fen string");
            return;
        }

        // send stop command to engine

        engineConnector.StopCalculating();

        // change from white to black

        mutex.WaitOne();
        {
            // change human color

            playerColor = color;

            // load fen

            board.LoadFEN(fen);

            engineConnectorMutex.WaitOne();
            {
                engineConnector.LoadFEN(fen);
            }
            engineConnectorMutex.ReleaseMutex();

            // update board ui

            DeselectPiece();
            boardGraphics.DeselectSquare();
            boardGraphics.StopAnimation();
            boardGraphics.FlipBoard(color == Piece.Color.Black);
            boardGraphics.UpdateSprites();

            // change game state

            gameState = GameState.PlayerMoving;
        }
        mutex.ReleaseMutex();

        // hide game end label

        gameEndLabel.Visible = false;

        // play sound

        sounds["GameStart"].Play();
    }

    public void SelectPromotionPieceType(Piece.Type type)
    {
        mutex.WaitOne();
        {
            board.PromotionPieceType = type;
        }
        mutex.ReleaseMutex();
    }

    public string GetFEN()
    {
        string fen;

        mutex.WaitOne();
        {
            fen = board.GetFEN();
        }
        mutex.ReleaseMutex();

        return fen;
    }

    public void SelectComputerELO(int elo)
    {
        engineConnector.LimitStrengthTo(elo);
    }

    public void FlipBoard()
    {
        boardGraphics.FlipBoard(!boardGraphics.IsBoardFlipped());
        boardGraphics.UpdateSprites();
    }

    // computer turn (this runs in a separated thread)

    private void ComputerTurn()
    {
        Board boardCopy = new Board();

        while (true)
        {
            // non busy wait until computer turn

            computerThreadBarrier.WaitOne();

            // check if in the middle of the move calculation a button that aborts this current operation is pressed

            bool aborted;

            // perform a copy of the current state of the board

            mutex.WaitOne();
            {
                aborted = gameState != GameState.WaitingForComputer;

                if (!aborted)
                {
                    board.CopyBoardState(boardCopy);
                }
            }
            mutex.ReleaseMutex();

            if (!aborted)
            {
                // get the chosen move using the copy of the board

                Move move;

                engineConnectorMutex.WaitOne();
                {
                    move = engineConnector.GetBestMove(boardCopy);
                }
                engineConnectorMutex.ReleaseMutex();

                // setup the move animation

                mutex.WaitOne();
                {
                    aborted = gameState != GameState.WaitingForComputer;

                    if (!aborted)
                    {
                        moveAnimation.move = move;
                        moveAnimation.dragged = false;
                        gameState = GameState.AnimateMove;
                    }
                }
                mutex.ReleaseMutex();
            }
        }
    }

    // Called when the node enters the scene tree for the first time.

    public override void _Ready()
	{
        // precalculate moves

        MoveGeneration.PrecalculateMoves();

        // get nodes

        boardGraphics = GetNode<BoardGraphics>("BoardGraphics");

        sounds["MoveSelf"] = GetNode<AudioStreamPlayer>("MoveSelfSound");
        sounds["MoveOpponent"] = GetNode<AudioStreamPlayer>("MoveOpponentSound");
        sounds["Capture"] = GetNode<AudioStreamPlayer>("CaptureSound");
        sounds["Castle"] = GetNode<AudioStreamPlayer>("CastleSound");
        sounds["Check"] = GetNode<AudioStreamPlayer>("CheckSound");
        sounds["Promote"] = GetNode<AudioStreamPlayer>("PromoteSound");
        sounds["GameStart"] = GetNode<AudioStreamPlayer>("GameStartSound");
        sounds["GameEnd"] = GetNode<AudioStreamPlayer>("GameEndSound");

        // connect board graphics to board

        boardGraphics.LoadPiecesTheme(piecesTextureAtlas);
        boardGraphics.LoadBoardTheme(boardTheme);
        boardGraphics.ConnectToBoard(board);

        // engine connector

        engineConnector.ConnectToEngine("Assets/ChessEngines/stockfish/stockfish.exe");
        engineConnector.LimitStrengthTo(computerElo);

        // load fens 4Q3/2B2Pp1/p5kp/P7/4q3/b1p4P/5PPK/4r3 w - -

        PlayAsColor(StartFEN, playerColor);

        // computer thread

        computerThread = new Thread(ComputerTurn);
        computerThread.Start();
    }

    // process notifications

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            engineConnector.Disconnect();
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.

    public override void _Process(double delta)
	{
        mutex.WaitOne();

        switch (gameState)
        {
            case GameState.AnimateMove:
                // make the move

                board.MakeMove(moveAnimation.move);
                engineConnector.SendMove(moveAnimation.move);

                // board graphics set up

                boardGraphics.DeselectPieceSquare();
                boardGraphics.DeselectSquare();
                boardGraphics.SetHintMoves(null);

                // perform the move animation for the selected move

                boardGraphics.AnimateMove(moveAnimation.move, moveAnimation.dragged, Callable.From(() =>
                {
                    gameState = GameState.AnimationJustFinished;
                }));

                // change state

                gameState = GameState.WaitingForAnimationToFinish;
                break;
            case GameState.AnimationJustFinished:
                // update sprites

                boardGraphics.UpdateSprites();

                // check for game ended 

                List<Move> availableMoves = MoveGeneration.GetAllLegalMovesByColor(board, board.GetTurnColor());
                bool isKingInCheck = MoveGeneration.IsKingInCheck(board, board.GetTurnColor());

                if (availableMoves.Count <= 0) // if there are no legal moves
                {
                    if (isKingInCheck) // if is the king in check then it is checkmate
                    {
                        gameEndLabel.Text = checkmateString;
                        gameEndLabel.Visible = true;
                        GD.Print("Checkmate!");
                    }
                    else // it is stalemate
                    {
                        gameEndLabel.Text = drawString;
                        gameEndLabel.Visible = true;
                        GD.Print("Draw!");
                    }

                    sounds["GameEnd"].Play();

                    // change game state

                    gameState = GameState.GameEnd;
                }
                else if (isKingInCheck) // if the king is checked
                {
                    sounds["Check"].Play();

                    // change game state

                    gameState = GameState.PlayerMoving;
                }
                else
                {
                    // play sounds

                    switch (moveAnimation.move.flags)
                    {
                        case Move.Flags.CastleShort:
                        case Move.Flags.CastleLong:
                            sounds["Castle"].Play();
                            break;
                        case Move.Flags.Promotion:
                            sounds["Promote"].Play();
                            break;
                        case Move.Flags.EnPassant:
                            sounds["Capture"].Play();
                            break;
                        default:
                            if (moveAnimation.move.pieceTarget.type != Piece.Type.None)
                            {
                                sounds["Capture"].Play();
                            }
                            else if (moveAnimation.move.pieceSource.color == playerColor)
                            {
                                sounds["MoveSelf"].Play();
                            }
                            else
                            {
                                sounds["MoveOpponent"].Play();
                            }
                            break;
                    }

                    // change game state

                    gameState = GameState.PlayerMoving;
                }
                break;
            case GameState.PlayerMoving:
                // get the turn color

                Piece.Color turnColor = board.GetTurnColor();

                // turnColor = Piece.Color.None;

                if (turnColor == playerColor)
                {
                    // human turn

                    // mouse position relative to the board graphics

                    Vector2 mousePosition = GetViewport().GetMousePosition() - boardGraphics.Position;

                    // get the square the mouse is at & calculate its index

                    bool isSquareInBoard = boardGraphics.TryGetSquareIndexFromCoords(mousePosition, out int squareIndex);

                    // select & deselect piece & make moves

                    if (Input.IsActionJustPressed("Click"))
                    {
                        if (isSquareInBoard)
                        {
                            if (pieceSelection.isSelected)
                            {
                                // check if it is in a legal move

                                bool isMoveLegal = false;

                                foreach (Move move in pieceSelection.legalMoves)
                                {
                                    if (move.squareTargetIndex == squareIndex)
                                    {
                                        // mark the move legal flag to true

                                        isMoveLegal = true;

                                        // deselect the piece

                                        DeselectPiece();

                                        // prepare animation

                                        moveAnimation.move = move;
                                        moveAnimation.dragged = false;
                                        gameState = GameState.AnimateMove;

                                        break;
                                    }
                                }

                                // check if the move is legal

                                if (!isMoveLegal)
                                {
                                    // check if other piece is selected

                                    Piece piece = board.GetPiece(squareIndex);

                                    if (piece.color == playerColor)
                                    {
                                        // get legal moves from the piece

                                        List<Move> legalMoves = MoveGeneration.GetLegalMoves(board, squareIndex);

                                        // select the piece

                                        SelectPiece(squareIndex, legalMoves);

                                        // board select the piece

                                        boardGraphics.SelectPiece(squareIndex);
                                        boardGraphics.SelectPieceSquare(squareIndex);
                                        boardGraphics.SetHintMoves(legalMoves);
                                    }
                                    else
                                    {
                                        // deselect the piece

                                        DeselectPiece();

                                        // remove the board hints & deselect the piece

                                        boardGraphics.DeselectPiece(squareIndex);
                                        boardGraphics.DeselectPieceSquare();
                                        boardGraphics.SetHintMoves(null);
                                    }
                                }
                            }
                            else
                            {
                                Piece piece = board.GetPiece(squareIndex);

                                if (piece.color == playerColor)
                                {
                                    // get legal moves from the piece

                                    List<Move> legalMoves = MoveGeneration.GetLegalMoves(board, squareIndex);

                                    // select the piece

                                    SelectPiece(squareIndex, legalMoves);

                                    // board select the piece

                                    boardGraphics.SelectPiece(squareIndex);
                                    boardGraphics.SelectPieceSquare(squareIndex);
                                    boardGraphics.SetHintMoves(legalMoves);
                                }
                            }
                        }
                        else
                        {
                            // deselect the piece

                            DeselectPiece();

                            // remove the board hints & deselect the piece

                            boardGraphics.DeselectPiece(squareIndex);
                            boardGraphics.DeselectPieceSquare();
                            boardGraphics.SetHintMoves(null);
                        }
                    }

                    // when the piece is being hold with the mouse

                    if (pieceSelection.isHolding)
                    {
                        Sprite2D pieceSprite = boardGraphics.GetPieceSprite(pieceSelection.squareIndex);
                        pieceSprite.Position = mousePosition;

                        if (isSquareInBoard)
                        {
                            boardGraphics.SelectSquare(squareIndex);
                        }
                    }
                    else
                    {
                        boardGraphics.DeselectSquare();
                    }

                    // when the mouse is released (if the piece was being hold things happen)

                    if (Input.IsActionJustReleased("Click"))
                    {
                        if (pieceSelection.isHolding)
                        {
                            if (isSquareInBoard)
                            {
                                // check if it is in a legal move

                                bool isMoveLegal = false;

                                foreach (Move move in pieceSelection.legalMoves)
                                {
                                    if (move.squareTargetIndex == squareIndex)
                                    {
                                        // mark the move legal flag to true

                                        isMoveLegal = true;

                                        // deselect the piece

                                        DeselectPiece();

                                        // prepare animation

                                        moveAnimation.move = move;
                                        moveAnimation.dragged = true;
                                        gameState = GameState.AnimateMove;

                                        break;
                                    }
                                }

                                if (!isMoveLegal)
                                {
                                    pieceSelection.isHolding = false;
                                    boardGraphics.UpdateSprites();
                                }
                            }
                            else
                            {
                                pieceSelection.isHolding = false;
                                boardGraphics.UpdateSprites();
                            }
                        }
                    }
                }
                else
                {
                    // computer turn

                    gameState = GameState.WaitingForComputer;
                    computerThreadBarrier.Release();
                }

                break;
        }

        mutex.ReleaseMutex();
	}
}

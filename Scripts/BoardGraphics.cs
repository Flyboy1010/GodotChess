using Godot;
using System;
using System.Collections.Generic;
using static Board;

public partial class BoardGraphics : Node2D
{
    // theme things

    private float pieceTextureSize; // size of a piece in the texture atlas
    private BoardTheme boardTheme; // current board theme

    private int squareSize = 105; // size of each board square
    private Sprite2D[] piecesSprites = new Sprite2D[64];
    private ColorRect[] hintsSprites = new ColorRect[64];
    private bool isBoardFlipped = false;
    private Board board;

    private int selectedSquareIndex;
    private bool showSelectedSquare;

    private int selectedPieceSquareIndex;
    private bool showSelectedPieceSquare;

    // tween for animating the pieces

    private Tween tween;

    // connect board with graphics

    public void ConnectToBoard(Board board)
    {
        this.board = board;
    }

    // get piece sprite

    public Sprite2D GetPieceSprite(int index)
    {
        return piecesSprites[index];
    }

    // select piece 

    public void SelectPiece(int index)
    {
        piecesSprites[index].ZIndex = 1;
    }

    public void DeselectPiece(int index)
    {
        piecesSprites[index].ZIndex = 0;
    }

    public void SelectSquare(int index)
    {
        selectedSquareIndex = index;
        showSelectedSquare = true;
    }

    public void DeselectSquare()
    {
        showSelectedSquare = false;
    }

    public void SelectPieceSquare(int index)
    {
        selectedPieceSquareIndex = index;
        showSelectedPieceSquare = true;
    }

    public void DeselectPieceSquare()
    {
        showSelectedPieceSquare = false;
    }

    // get square index at x,y world coordinates (FIXME?)

    public bool TryGetSquareIndexFromCoords(Vector2 coords, out int squareIndex)
    {
        Vector2I square = (Vector2I)(coords / squareSize).Floor();

        squareIndex = square.X + square.Y * 8;

        if (isBoardFlipped)
        {
            squareIndex = 63 - squareIndex;
        }

        return (square.X >= 0 && square.X < 8 && square.Y >= 0 && square.Y < 8);
    }

    // create the pieces sprites

    private void CreateGraphics()
    {
        // create hints sprites

        for (int j = 0; j < 8; j++)
        {
            for (int i = 0; i < 8; i++)
            {
                ColorRect hintSprite = new ColorRect();
                hintSprite.Size = new Vector2(squareSize, squareSize);
                hintSprite.Position = new Vector2(i, j) * squareSize;
                hintSprite.Visible = false;

                hintsSprites[i + j * 8] = hintSprite;
                AddChild(hintSprite);
            }
        }

        // create pieces sprites

        for (int j = 0; j < 8; j++)
        {
            for (int i = 0; i < 8; i++)
            {
                Sprite2D pieceSprite = new Sprite2D();
                pieceSprite.RegionEnabled = true;
                pieceSprite.Visible = false;
                pieceSprite.ZIndex = 0;
                pieceSprite.TextureFilter = TextureFilterEnum.LinearWithMipmapsAnisotropic;

                piecesSprites[i + j * 8] = pieceSprite;
                AddChild(pieceSprite);
            }
        }
    }

    // load theme

    public void LoadPiecesTheme(Texture2D piecesTextureAtlas)
    {
        pieceTextureSize = piecesTextureAtlas.GetSize().Y / 2.0f;
        float scale = squareSize / pieceTextureSize;

        for (int j = 0; j < 8; j++)
        {
            for (int i = 0; i < 8; i++)
            {
                Sprite2D pieceSprite = piecesSprites[i + j * 8];
                pieceSprite.Texture = piecesTextureAtlas;
                pieceSprite.Scale = new Vector2(scale, scale);
            }
        }
    }

    public void LoadBoardTheme(BoardTheme theme)
    {
        boardTheme = theme;
    }

    // set piece

    private void SetSpritePiece(Sprite2D sprite, Piece piece)
    {
        if (piece.type == Piece.Type.None)
        {
            sprite.Visible = false;
        }
        else
        {
            sprite.RegionRect = new Rect2(pieceTextureSize * ((int)piece.type - 1), pieceTextureSize * ((int)piece.color - 1), pieceTextureSize, pieceTextureSize);
            sprite.Visible = true;
        }
    }

    // update sprites

    public void UpdateSprites()
    {
        for (int j = 0; j < 8; j++)
        {
            for (int i = 0; i < 8; i++)
            {
                int index = i + j * 8;

                // pieces sprites

                Sprite2D pieceSprite = piecesSprites[index];
                pieceSprite.ZIndex = 0;
                Piece piece = board.GetPiece(index);

                SetSpritePiece(pieceSprite, piece);

                Vector2 pieceSpritePosition = isBoardFlipped ? new Vector2((7 - i) + 0.5f, (7 - j) + 0.5f) : new Vector2(i + 0.5f, j + 0.5f);

                pieceSprite.Position = pieceSpritePosition * squareSize;

                // hint moves sprites

                ColorRect hintSprite = hintsSprites[index];
                Vector2 hintSpritePosition = isBoardFlipped ? new Vector2(7 - i, 7 - j) : new Vector2(i, j);
                hintSprite.Position = hintSpritePosition * squareSize;
            }
        }
    }

    // set hint moves

    public void SetHintMoves(List<Move> moves)
    {
        for (int i = 0; i < 64; i++)
        {
            hintsSprites[i].Visible = false;
        }

        if (moves != null)
        {
            foreach (Move move in moves)
            {
                Material m = move.pieceTarget.type == Piece.Type.None ? AssetsManager.CircleMaterial : AssetsManager.CircleHoleMaterial;

                hintsSprites[move.squareTargetIndex].Material = m;
                hintsSprites[move.squareTargetIndex].Visible = true;
            }
        }
    }

    // flip board

    public void FlipBoard(bool flip)
    {
        isBoardFlipped = flip;
    }

    public bool IsBoardFlipped()
    {
        return isBoardFlipped;
    }

    // animate move

    public void AnimateMove(Move move, bool isPieceDragged, Callable onFinishCallback)
    {
        if (tween != null)
        {
            tween.Kill();
        }

        tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Quad);
        tween.SetParallel(true);

        float animationTime = 0.20f;

        // get the sprite

        Sprite2D pieceSprite = piecesSprites[move.squareSourceIndex];
        pieceSprite.ZIndex = 1;

        // calculate position of the source square

        int i = move.squareTargetIndex % 8;
        int j = move.squareTargetIndex / 8;

        if (isBoardFlipped)
        {
            i = 7 - i;
            j = 7 - j;
        }

        if (!isPieceDragged)
        {
            // tween position to the target square

            tween.TweenProperty(pieceSprite, "position", new Vector2(i + 0.5f, j + 0.5f) * squareSize, animationTime);
        }
        else
        {
            // instantly move the piece to that square

            pieceSprite.Position = new Vector2(i + 0.5f, j + 0.5f) * squareSize;
        }

        // check if the move is castling

        if (move.flags == Move.Flags.CastleShort)
        {
            int rookTargetSquareIndex = 0;
            Sprite2D towerPieceSprite = null;

            switch (move.pieceSource.color)
            {
                case Piece.Color.White:
                    rookTargetSquareIndex = F1;
                    towerPieceSprite = piecesSprites[H1];
                    break;
                case Piece.Color.Black:
                    rookTargetSquareIndex = F8;
                    towerPieceSprite = piecesSprites[H8];
                    break;
            }

            towerPieceSprite.ZIndex = 2;

            int rook_i = rookTargetSquareIndex % 8;
            int rook_j = rookTargetSquareIndex / 8;

            if (isBoardFlipped)
            {
                rook_i = 7 - rook_i;
                rook_j = 7 - rook_j;
            }

            tween.TweenProperty(towerPieceSprite, "position", new Vector2(rook_i + 0.5f, rook_j + 0.5f) * squareSize, animationTime);
        }
        else if (move.flags == Move.Flags.CastleLong)
        {
            int rookTargetSquareIndex = 0;
            Sprite2D towerPieceSprite = null;

            switch (move.pieceSource.color)
            {
                case Piece.Color.White:
                    rookTargetSquareIndex = D1;
                    towerPieceSprite = piecesSprites[A1];
                    break;
                case Piece.Color.Black:
                    rookTargetSquareIndex = D8;
                    towerPieceSprite = piecesSprites[A8];
                    break;
            }

            towerPieceSprite.ZIndex = 2;

            int rook_i = rookTargetSquareIndex % 8;
            int rook_j = rookTargetSquareIndex / 8;

            if (isBoardFlipped)
            {
                rook_i = 7 - rook_i;
                rook_j = 7 - rook_j;
            }

            tween.TweenProperty(towerPieceSprite, "position", new Vector2(rook_i + 0.5f, rook_j + 0.5f) * squareSize, animationTime);
        }

        // at the end of the animation update the pieces & call on finish callback

        tween.Chain().TweenCallback(onFinishCallback);
    }

    // stop the current animation

    public void StopAnimation()
    {
        if (tween != null)
        {
            tween.Kill();
        }
    }

    // Called when the node enters the scene tree for the first time.

    public override void _Ready()
	{
        // Create graphics

        CreateGraphics();
	}

    // draw

    public override void _Draw()
    {
        // draw the actual board

        for (int j = 0; j < 8; j++)
        {
            for (int i = 0; i < 8; i++)
            {
                Color squareColor = (i + j) % 2 == 0 ? boardTheme.LightColor : boardTheme.DarkColor;

                DrawRect(new Rect2(i * squareSize, j * squareSize, squareSize, squareSize), squareColor, true);
            }
        }

        // draw last move

        if (board.TryGetLastMove(out Move lastMove))
        {
            int s_i = lastMove.squareSourceIndex % 8;
            int s_j = lastMove.squareSourceIndex / 8;
            int t_i = lastMove.squareTargetIndex % 8;
            int t_j = lastMove.squareTargetIndex / 8;

            if (isBoardFlipped)
            {
                s_i = 7 - s_i;
                s_j = 7 - s_j;
                t_i = 7 - t_i;
                t_j = 7 - t_j;
            }

            DrawRect(new Rect2(new Vector2(s_i, s_j) * squareSize, squareSize, squareSize), boardTheme.LastMoveColor);
            DrawRect(new Rect2(new Vector2(t_i, t_j) * squareSize, squareSize, squareSize), boardTheme.LastMoveColor);
        }

        // draw selected piece square

        if (showSelectedPieceSquare)
        {
            int i = selectedPieceSquareIndex % 8;
            int j = selectedPieceSquareIndex / 8;

            if (isBoardFlipped)
            {
                i = 7 - i;
                j = 7 - j;
            }

            DrawRect(new Rect2(i * squareSize, j * squareSize, squareSize, squareSize), boardTheme.LastMoveColor);
        }

        // draw selected square

        if (showSelectedSquare)
        {
            int i = selectedSquareIndex % 8;
            int j = selectedSquareIndex / 8;

            if (isBoardFlipped)
            {
                i = 7 - i;
                j = 7 - j;
            }

            DrawRect(new Rect2(i * squareSize + 3, j * squareSize + 3, squareSize - 6, squareSize - 6), new Color(1.0f, 1.0f, 1.0f, 0.65f), false, 6);
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.

    public override void _Process(double delta)
	{
        // redraw every frame

        QueueRedraw();
	}
}

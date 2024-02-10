using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class Board
{
	// piece struct

	public struct Piece
	{
        public enum Type
        {
            None,
            King,
            Queen,
            Bishop,
            Knight,
            Rook,
            Pawn
        }

        public enum Color
		{
            None,
            White,
			Black
        }

        public Type type;
        public Color color;
	}

	// move struct

	public struct Move
	{
		// move flags

		public enum Flags
		{
			None,
			DoublePush,
			Promotion,
			EnPassant,
			CastleShort,
			CastleLong
		}

		// for every move

		public int squareSourceIndex, squareTargetIndex;
		public Piece pieceSource, pieceTarget;
		public Flags flags;

		// promotion

		public Piece.Type promotionPieceType;
	}

	// state struct

	public struct State
	{
        public Piece.Color turnColor;
		public Piece.Color doublePushedPawnColor; // color of pawn that doublepushed for check if enPassant is available
		public int enPassantSquareIndex; // pawn tile that can be captured by en-passant
		public bool canCastleWhite, canCastleShortWhite, canCastleLongWhite;
        public bool canCastleBlack, canCastleShortBlack, canCastleLongBlack;
    }

	// start fen string

	public static readonly string StartFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq -";

	// towers squares & king squares

	public static readonly int A1 = 56, H1 = 63, A8 = 0, H8 = 7; // tower squares
	public static readonly int E1 = 60, E8 = 4; // king squares
	public static readonly int C1 = 58, C8 = 2, G1 = 62, G8 = 6;
	public static readonly int F1 = 61, D1 = 59, F8 = 5, D8 = 3; // tower castling target squares
    public static readonly int B1 = 57, B8 = 1;

    // promotion piece

    public Piece.Type PromotionPieceType = Piece.Type.Queen;

	// pieces array
	// TODO: make a list that tracks the indices where the pieces are instead of just looping through all the board to find them

	private Piece[] pieces = new Piece[64];
	private List<int> whitePiecesIndices = new List<int>();
	private List<int> blackPiecesIndices = new List<int>();

	// stack holding the moves and the states of the game as moves are been played

	private Stack<Move> moves = new Stack<Move>();
	private Stack<State> states = new Stack<State>();

	private State currentState = new State();

	// get piece at index

    public Piece GetPiece(int index)
	{
		return pieces[index];
	}

	// get pieces indices from color

	public List<int> GetPiecesIndicesByColor(Piece.Color color)
	{
		switch (color)
		{
			case Piece.Color.White:
				return whitePiecesIndices;
			case Piece.Color.Black:
				return blackPiecesIndices;
		}

		return null;
	}

	// get last move

	public bool TryGetLastMove(out Move move)
	{
		return moves.TryPeek(out move);
	}

	// get state

	public ref readonly State GetState()
	{
		return ref currentState;
	}

	// get turn color

	public Piece.Color GetTurnColor()
	{
		return currentState.turnColor;
	}

    // find king

    public int FindKingOfColor(Piece.Color color)
    {
		List<int> indices = GetPiecesIndicesByColor(color);

		foreach (int index in indices)
		{
			if (pieces[index].type == Piece.Type.King)
			{
				return index;
			}
		}

        GD.PrintErr("there is no king?");

        return 0;
    }

    // copy board pieces and current state to other board

    public void CopyBoardState(Board board)
	{
		pieces.CopyTo(board.pieces, 0);
		board.currentState = currentState;
		board.whitePiecesIndices = whitePiecesIndices.ToList();
        board.blackPiecesIndices = blackPiecesIndices.ToList();
    }

	// set fen string

	public void LoadFEN(string fen)
	{
        // clear

        states.Clear();
        moves.Clear();
		whitePiecesIndices.Clear();
		blackPiecesIndices.Clear();

        // split fen string

        string[] subFEN = fen.Split(' ');

		// pieces placement (subFEN[0])

		Dictionary<char, Piece.Type> pieceTypeFromSymbol = new Dictionary<char, Piece.Type>()
		{
			{ 'p', Piece.Type.Pawn }, { 'n', Piece.Type.Knight }, { 'b', Piece.Type.Bishop },
			{ 'r', Piece.Type.Rook }, { 'q', Piece.Type.Queen  }, { 'k', Piece.Type.King   }
		};

		int i = 0, j = 0;

		foreach (char symbol in subFEN[0])
		{
			if (symbol == '/')
			{
				i = 0;
				j++;
			}
			else if (char.IsDigit(symbol))
			{
				int n = symbol - '0'; // number of empy squares

				for (int ii = 0; ii < n; ii++)
				{
					pieces[(i + ii) + j * 8] = new Piece(); // "none" piece
				}

				i += n;
			}
			else
			{
				int index = i + j * 8;
				Piece.Type pieceType = pieceTypeFromSymbol[char.ToLower(symbol)]; // must be a piece symbol then
				Piece.Color pieceColor = char.IsUpper(symbol) ? Piece.Color.White : Piece.Color.Black;
				pieces[index] = new Piece { type = pieceType, color = pieceColor };
				i++;

				// pieces list

				switch (pieceColor)
				{
					case Piece.Color.White:
						whitePiecesIndices.Add(index);
						break;
					case Piece.Color.Black:
						blackPiecesIndices.Add(index);
						break;
				}
			}
		}

		// turn color (subFEN[1])

		currentState.turnColor = subFEN[1].Equals("w") ? Piece.Color.White : Piece.Color.Black;

        // castling rights (subFEN[2])

        currentState.canCastleShortWhite = false;
        currentState.canCastleLongWhite = false;
        currentState.canCastleShortBlack = false;
        currentState.canCastleLongBlack = false;

        if (subFEN[2].Equals("-"))
		{
			// neither side cant castle

			currentState.canCastleWhite = false;
            currentState.canCastleBlack = false;
        }
		else
		{
			foreach (char symbol in subFEN[2])
			{
				switch (symbol)
				{
					case 'K':
						currentState.canCastleShortWhite = true;
						break;
					case 'Q':
                        currentState.canCastleLongWhite = true;
                        break;
					case 'k':
                        currentState.canCastleShortBlack = true;
                        break;
					case 'q':
                        currentState.canCastleLongBlack = true;
                        break;
				}
			}

			currentState.canCastleWhite = currentState.canCastleShortWhite || currentState.canCastleLongWhite;
            currentState.canCastleBlack = currentState.canCastleShortBlack || currentState.canCastleLongBlack;
        }

		// en passant (subFEN[3])

		if (subFEN[3].Equals("-"))
		{
			currentState.doublePushedPawnColor = Piece.Color.None;
			currentState.enPassantSquareIndex = 0;
		}
		else
		{
			// the format is then the char of the column & the number of the row

			int column = subFEN[3][0] - 'a';
			int row = 0;

			switch (subFEN[3][1])
			{
				case '3':
					row = 4;
					currentState.doublePushedPawnColor = Piece.Color.White;
					break;
				case '6':
					row = 3;
                    currentState.doublePushedPawnColor = Piece.Color.Black;
                    break;
			}

			currentState.enPassantSquareIndex = column + row * 8;
		}
	}

	// get fen

	public string GetFEN()
	{
        Dictionary<Piece.Type, char> symbolFromPieceType = new Dictionary<Piece.Type, char>()
        {
            { Piece.Type.Pawn, 'p' }, { Piece.Type.Knight, 'n' }, { Piece.Type.Bishop, 'b' },
            { Piece.Type.Rook, 'r' }, { Piece.Type.Queen , 'q' }, { Piece.Type.King  , 'k' }
        };

        StringBuilder fenString = new StringBuilder();

		// pieces

		for (int j = 0; j < 8; j++)
		{
			int emptyCounter = 0;

			for (int i = 0; i < 8; i++)
			{
				int index = i + j * 8;

				Piece piece = pieces[index];

				if (piece.type != Piece.Type.None)
				{
					if (emptyCounter != 0)
					{
						fenString.Append(emptyCounter);
					}

					char pieceSymbol = piece.color == Piece.Color.White ? char.ToUpper(symbolFromPieceType[piece.type]) : symbolFromPieceType[piece.type];
                    fenString.Append(pieceSymbol);

					emptyCounter = 0;
				}
				else
				{
					emptyCounter++;
				}
			}

			if (emptyCounter != 0)
			{
                fenString.Append(emptyCounter);
            }

			if (j < 7)
			{
				fenString.Append('/');
			}
		}

		// turn color

		char turnColor = currentState.turnColor == Piece.Color.White ? 'w' : 'b';
		fenString.AppendFormat(" {0} ", turnColor);

		// castling rights

		if (currentState.canCastleWhite || currentState.canCastleBlack)
		{
			if (currentState.canCastleWhite)
			{
				if (currentState.canCastleShortWhite)
				{
					fenString.Append('K');
				}

				if (currentState.canCastleLongWhite)
				{
					fenString.Append('Q');
				}
			}

			if (currentState.canCastleBlack)
			{
				if (currentState.canCastleShortBlack)
				{
					fenString.Append('k');
				}

				if (currentState.canCastleLongBlack)
				{
					fenString.Append('q');
				}
			}
        }
		else
		{
			fenString.Append("-");
		}

		// en passant

		if (currentState.doublePushedPawnColor != Piece.Color.None)
		{
            char[] letters = { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };
            int column = currentState.enPassantSquareIndex % 8;
			char columnLetter = letters[column];

			switch (currentState.doublePushedPawnColor)
			{
				case Piece.Color.White:
					fenString.AppendFormat(" {0}{1}", columnLetter, 4);
					break;
				case Piece.Color.Black:
                    fenString.AppendFormat(" {0}{1}", columnLetter, 6);
                    break;
			}
		}
		else
		{
			fenString.Append(" -");
		}

        return fenString.ToString();
	}

	// make a move on the board

	public void MakeMove(Move move)
	{
		// push the current state into the stack

		states.Push(currentState);

        // modify pieces indices list

        switch (move.pieceSource.color)
        {
            case Piece.Color.White:
                whitePiecesIndices.Remove(move.squareSourceIndex);
                whitePiecesIndices.Add(move.squareTargetIndex);
                break;
            case Piece.Color.Black:
                blackPiecesIndices.Remove(move.squareSourceIndex);
                blackPiecesIndices.Add(move.squareTargetIndex);
                break;
        }

        switch (move.pieceTarget.color)
        {
            case Piece.Color.White:
                whitePiecesIndices.Remove(move.squareTargetIndex);
                break;
            case Piece.Color.Black:
                blackPiecesIndices.Remove(move.squareTargetIndex);
                break;
        }

        // make the move & change state

        pieces[move.squareSourceIndex] = new Piece(); // remove piece at the "Source" square

		// move flags

        switch (move.flags)
		{
			case Move.Flags.DoublePush:
				currentState.doublePushedPawnColor = move.pieceSource.color;
				currentState.enPassantSquareIndex = move.squareTargetIndex;
                pieces[move.squareTargetIndex] = move.pieceSource;
                break;
			case Move.Flags.Promotion:
                currentState.doublePushedPawnColor = Piece.Color.None;
                pieces[move.squareTargetIndex] = new Piece { type = move.promotionPieceType, color = move.pieceSource.color };
				break;
			case Move.Flags.EnPassant:
                switch (currentState.doublePushedPawnColor)
                {
                    case Piece.Color.White:
                        whitePiecesIndices.Remove(currentState.enPassantSquareIndex);
                        break;
                    case Piece.Color.Black:
                        blackPiecesIndices.Remove(currentState.enPassantSquareIndex);
                        break;
                }

                pieces[currentState.enPassantSquareIndex] = new Piece();
                currentState.doublePushedPawnColor = Piece.Color.None;
                pieces[move.squareTargetIndex] = move.pieceSource;

                break;
			case Move.Flags.CastleShort:
				currentState.doublePushedPawnColor = Piece.Color.None;
				pieces[move.squareSourceIndex] = new Piece();
				pieces[move.squareTargetIndex] = move.pieceSource;

				switch (move.pieceSource.color)
				{
					case Piece.Color.White:
						pieces[F1] = pieces[H1];
                        pieces[H1] = new Piece();

						whitePiecesIndices.Remove(H1);
						whitePiecesIndices.Add(F1);
                        break;
					case Piece.Color.Black:
                        pieces[F8] = pieces[H8];
                        pieces[H8] = new Piece();

                        blackPiecesIndices.Remove(H8);
                        blackPiecesIndices.Add(F8);
                        break;
				}
				break;
			case Move.Flags.CastleLong:
                currentState.doublePushedPawnColor = Piece.Color.None;
                pieces[move.squareSourceIndex] = new Piece();
                pieces[move.squareTargetIndex] = move.pieceSource;

                switch (move.pieceSource.color)
                {
                    case Piece.Color.White:
                        pieces[D1] = pieces[A1];
                        pieces[A1] = new Piece();

                        whitePiecesIndices.Remove(A1);
                        whitePiecesIndices.Add(D1);
                        break;
                    case Piece.Color.Black:
                        pieces[D8] = pieces[A8];
                        pieces[A8] = new Piece();

                        blackPiecesIndices.Remove(A8);
                        blackPiecesIndices.Add(D8);
                        break;
                }
                break;
            default:
                currentState.doublePushedPawnColor = Piece.Color.None;
                pieces[move.squareTargetIndex] = move.pieceSource;
                break;
		}

        // check if the king or the towers moved

        if (move.squareSourceIndex == E1 || move.squareTargetIndex == E1) // white king
		{
			currentState.canCastleWhite = false;
		}
		else if (move.squareSourceIndex == E8 || move.squareTargetIndex == E8) // black king
		{
			currentState.canCastleBlack = false;
		}
		
		if (move.squareSourceIndex == A1 || move.squareTargetIndex == A1) // white queen side tower
		{
			currentState.canCastleLongWhite = false;
		}
		else if (move.squareSourceIndex == H1 || move.squareTargetIndex == H1) // white king side tower
		{
			currentState.canCastleShortWhite = false;
		}

        if (move.squareSourceIndex == A8 || move.squareTargetIndex == A8) // black queen side tower
        {
            currentState.canCastleLongBlack = false;
        }
        else if (move.squareSourceIndex == H8 || move.squareTargetIndex == H8) // black king side tower
        {
            currentState.canCastleShortBlack = false;
        }

        // push the move into the stack

        moves.Push(move);

		// change turn color

		currentState.turnColor = currentState.turnColor == Piece.Color.White ? Piece.Color.Black : Piece.Color.White;
    }

	public void UndoMove()
	{
		if (states.Count > 0)
		{
			// get back to the last state

			currentState = states.Pop();

			// get the last move and undo it

			Move move = moves.Pop();

            // modify pieces indices list

            switch (move.pieceSource.color)
            {
                case Piece.Color.White:
                    whitePiecesIndices.Remove(move.squareTargetIndex);
                    whitePiecesIndices.Add(move.squareSourceIndex);
                    break;
                case Piece.Color.Black:
                    blackPiecesIndices.Remove(move.squareTargetIndex);
                    blackPiecesIndices.Add(move.squareSourceIndex);
                    break;
            }

            switch (move.pieceTarget.color)
            {
                case Piece.Color.White:
                    whitePiecesIndices.Add(move.squareTargetIndex);
                    break;
                case Piece.Color.Black:
                    blackPiecesIndices.Add(move.squareTargetIndex);
                    break;
            }

			// undo move

            pieces[move.squareSourceIndex] = move.pieceSource;
            pieces[move.squareTargetIndex] = move.pieceTarget;

            switch (move.flags)
			{
                case Move.Flags.CastleShort:
                    switch (move.pieceSource.color)
                    {
                        case Piece.Color.White:
                            pieces[F1] = new Piece();
                            pieces[H1] = new Piece { type = Piece.Type.Rook, color = Piece.Color.White };

                            whitePiecesIndices.Remove(F1);
                            whitePiecesIndices.Add(H1);
                            break;
                        case Piece.Color.Black:
                            pieces[F8] = new Piece();
                            pieces[H8] = new Piece { type = Piece.Type.Rook, color = Piece.Color.Black };

                            blackPiecesIndices.Remove(F8);
                            blackPiecesIndices.Add(H8);
                            break;
                    }
                    break;
                case Move.Flags.CastleLong:
                    switch (move.pieceSource.color)
                    {
                        case Piece.Color.White:
							pieces[D1] = new Piece();
                            pieces[A1] = new Piece { type = Piece.Type.Rook, color = Piece.Color.White };

                            whitePiecesIndices.Remove(D1);
                            whitePiecesIndices.Add(A1);
                            break;
                        case Piece.Color.Black:
							pieces[D8] = new Piece();
                            pieces[A8] = new Piece { type = Piece.Type.Rook, color = Piece.Color.Black };

                            blackPiecesIndices.Remove(D8);
                            blackPiecesIndices.Add(A8);
                            break;
                    }
                    break;
                case Move.Flags.EnPassant:
					pieces[currentState.enPassantSquareIndex] = new Piece { type = Piece.Type.Pawn, color = currentState.doublePushedPawnColor };

                    switch (currentState.doublePushedPawnColor)
                    {
                        case Piece.Color.White:
                            whitePiecesIndices.Add(currentState.enPassantSquareIndex);
                            break;
                        case Piece.Color.Black:
                            blackPiecesIndices.Add(currentState.enPassantSquareIndex);
                            break;
                    }
                    break;
			}
        }
	}
}

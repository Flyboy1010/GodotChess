using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using static Board;

public class EngineConnector
{
    // engine process related

    private Process engineProcess = new Process();
    private StreamReader engineProcessStdOut;
    private StreamWriter engineProcessStdIn;

    // list with all the moves

    private List<string> moves = new List<string>();

    // fen string, move time ...

    private string fenString = StartFEN;
    public int MoveTime = 1000; // in ms

    // for multithreading

    private System.Threading.Mutex mutex = new System.Threading.Mutex();

    // connect to a chess engine

    public void ConnectToEngine(string enginePath)
    {
        // engine process start info

        engineProcess.StartInfo.FileName = enginePath;
        engineProcess.StartInfo.UseShellExecute = false;
        engineProcess.StartInfo.RedirectStandardOutput = true;
        engineProcess.StartInfo.RedirectStandardInput = true;
        engineProcess.StartInfo.CreateNoWindow = true;

        // start engine process
        
        try
        {
            engineProcess.Start();

            // set std input and output of the child process

            engineProcessStdOut = engineProcess.StandardOutput;
            engineProcessStdIn = engineProcess.StandardInput;

            GD.Print("Connected to engine: ", enginePath);
        }
        catch (Exception e)
        {
            GD.Print(e.Message);
        }
    }

    public void Disconnect()
    {
        // send quit to engine and wait for exit

        mutex.WaitOne();
        {
            engineProcessStdIn.WriteLine("quit");
        }
        mutex.ReleaseMutex();

        engineProcess.WaitForExit();

        GD.Print("Disconnected from engine");
    }

    public void LimitStrengthTo(int eloValue)
    {
        mutex.WaitOne();
        {
            if (eloValue != int.MaxValue)
            {
                string command = string.Format("setoption name UCI_LimitStrength value true\nsetoption name UCI_Elo value {0}\n", eloValue);
                engineProcessStdIn.WriteLine(command);
            }
            else
            {
                engineProcessStdIn.WriteLine("setoption name UCI_LimitStrength value false\n");
            }
        }
        mutex.ReleaseMutex();
    }

    public void StopCalculating()
    {
        mutex.WaitOne();
        {
            engineProcessStdIn.WriteLine("stop");
        }
        mutex.ReleaseMutex();
    }

    public void LoadFEN(string fen)
    {
        fenString = fen;
        moves.Clear();
    }

    private string FromMoveToString(Move move)
    {
        // notation

        Dictionary<Piece.Type, char> symbolFromPieceType = new Dictionary<Piece.Type, char>()
        {
            { Piece.Type.Pawn, 'p' }, { Piece.Type.Knight, 'n' }, { Piece.Type.Bishop, 'b' },
            { Piece.Type.Rook, 'r' }, { Piece.Type.Queen , 'q' }, { Piece.Type.King  , 'k' }
        };

        char[] letters = { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };

        // get squares

        int squareSourceI = move.squareSourceIndex % 8;
        int squareSourceJ = move.squareSourceIndex / 8;
        int squareTargetI = move.squareTargetIndex % 8;
        int squareTargetJ = move.squareTargetIndex / 8;

        // str move

        string strMove = string.Format("{0}{1}{2}{3}", letters[squareSourceI], 8 - squareSourceJ, letters[squareTargetI], 8 - squareTargetJ);

        // if needs to add the promotion piece

        if (move.flags == Move.Flags.Promotion)
        {
            strMove += symbolFromPieceType[move.promotionPieceType];
        }

        // return the move

        return strMove;
    }

    private Move FromStringToMove(Board board, string strMove)
    {
        Dictionary<char, Piece.Type> pieceTypeFromSymbol = new Dictionary<char, Piece.Type>()
        {
            { 'p', Piece.Type.Pawn }, { 'n', Piece.Type.Knight }, { 'b', Piece.Type.Bishop },
            { 'r', Piece.Type.Rook }, { 'q', Piece.Type.Queen  }, { 'k', Piece.Type.King   }
        };

        // source tile

        int squareSourceI = strMove[0] - 'a';
        int squareSourceJ = 7 - (strMove[1] - '1');
        int squareSourceIndex = squareSourceI + squareSourceJ * 8;

        // targetTile

        int squareTargetI = strMove[2] - 'a';
        int squareTargetJ = 7 - (strMove[3] - '1');
        int squareTargetIndex = squareTargetI + squareTargetJ * 8;

        // get moves

        List<Move> moves = MoveGeneration.GetPseudoLegalMoves(board, squareSourceIndex); // assumption that the engine wont choose a non legal move
        Move chosenMove = new Move();

        foreach (Move move in moves)
        {
            if (move.squareTargetIndex == squareTargetIndex)
            {
                chosenMove = move;
                break;
            }
        }

        if (chosenMove.flags == Move.Flags.Promotion)
        {
            chosenMove.promotionPieceType = pieceTypeFromSymbol[strMove[4]];
        }

        return chosenMove;
    }

    public void SendMove(Move move)
    {
        string strMove = FromMoveToString(move);
        moves.Add(strMove);
        GD.Print("move: ", strMove);
    }

    public Move GetBestMove(Board board)
    {
        // construct the moves string

        StringBuilder command = new StringBuilder();

        command.AppendFormat("position fen {0} moves", fenString);

        foreach (string strMove in moves)
        {
            command.AppendFormat(" {0}", strMove);
        }

        command.AppendFormat("\ngo movetime {0}\n", MoveTime);

        // write to the engine process std in

        mutex.WaitOne();
        {
            engineProcessStdIn.Write(command);
        }
        mutex.ReleaseMutex();

        // read the output from the engine until it found the move

        bool moveFound = false;
        string bestMoveString = null;

        do
        {
            string engineOutputLine = engineProcessStdOut.ReadLine();

            if (engineOutputLine.Contains("bestmove"))
            {
                string[] bestMoveLine = engineOutputLine.Split(' ');
                bestMoveString = bestMoveLine[1];
                moveFound = true;
            }

        } while (!moveFound);

        // from bestmovestring to actual move

        Move bestMove = FromStringToMove(board, bestMoveString);

        return bestMove;
    }
}

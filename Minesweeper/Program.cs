using Mindmagma.Curses;
using System;
using System.Diagnostics;
using System.Threading;

class Mines
{
    struct Minefield_s
    {
        public bool isMine;
        public int borderingMineCount;
        public bool isOpened;
        public bool isFlagged;
    }

    private readonly int ScreenHeight;
    private readonly int ScreenWidth;
    private readonly int TotalMines;
    private Minefield_s[,] Minefield;
    private List<int[]> mineLocations;

    public Mines(int screenHeight, int screenWidth, int totalMines) 
    {
        ScreenHeight = screenHeight; 
        ScreenWidth = screenWidth;
        TotalMines = totalMines;
        Minefield = new Minefield_s[screenHeight, screenWidth];
    }

    // Randomly assign mines to the minefield
    public void Add_Mines(int[] exclude)
    {
        Random random = new();

        // Get random locations and save them to a list
        List<int[]> randomLocations = [];
        for (int i = 0; i < TotalMines; i++)
        {
            int randomRow;
            int randomCol;
            bool isDuplicateOrExclude = false;

            do
            {
                randomRow = random.Next(0, ScreenHeight);
                randomCol = random.Next(0, ScreenWidth);

                if (exclude.SequenceEqual([randomRow, randomCol]))
                {
                    isDuplicateOrExclude = true;
                    continue;
                }

                foreach (int[] randomLocation in randomLocations)
                {
                    if (randomLocation.SequenceEqual([randomRow, randomCol]))
                    {
                        isDuplicateOrExclude = true;
                        break;
                    }
                    else
                    {
                        isDuplicateOrExclude = false;
                    }
                }
            } while (isDuplicateOrExclude);

            randomLocations.Add([randomRow, randomCol]);
        }

        // Make randomly selected tiles mines
        foreach (int[] randomLocation in randomLocations)
        {
            Minefield[randomLocation[0], randomLocation[1]].isMine = true;
        }

        mineLocations = randomLocations;
    }

    // Count mines that the tile borders with
    public void CountMineBordering()
    {
        foreach (int[] mineLocation in mineLocations)
        {
            int y = mineLocation[0];
            int x = mineLocation[1];
            for (int i = y - 1; i < y + 2; i++)
            {
                for (int j = x - 1; j < x + 2; j++)
                {
                    if (i >= 0 && j >= 0 && i < ScreenHeight && j < ScreenWidth && !Minefield[i, j].isMine)
                    {
                        Minefield[i, j].borderingMineCount++;
                    }
                }
            }
        }
    }

    public void DisplayMines(nint minefieldScreen, int[] playerPositionYX, bool game_ended)
    {
        for (int i = 0; i < ScreenHeight; i++)
        {
            for (int j = 0; j < ScreenWidth; j++)
            {
                int colorPair = Minefield[i, j].borderingMineCount;
                colorPair = playerPositionYX.SequenceEqual([i, j]) ? colorPair + 9 : colorPair;

                if (Minefield[i, j].isFlagged && !Minefield[i, j].isOpened)
                {
                    colorPair = 18;
                }
                else if (Minefield[i, j].isMine && game_ended)
                {
                    colorPair = 19;
                }

                if (Minefield[i, j].isOpened)
                {
                    NCurses.WindowAttributeOn(minefieldScreen, NCurses.ColorPair(colorPair));
                    colorPair = playerPositionYX.SequenceEqual([i, j]) ? colorPair - 9 : colorPair;
                    NCurses.MoveWindowAddString(minefieldScreen, i + 1, j + 1, colorPair.ToString().Replace('0', '.'));
                    colorPair = playerPositionYX.SequenceEqual([i, j]) ? colorPair + 9 : colorPair;
                    NCurses.WindowAttributeOff(minefieldScreen, NCurses.ColorPair(colorPair));
                }
                else
                {
                    if (colorPair >= 1 &&  colorPair <= 8)
                    {
                        colorPair = 0;
                    }
                    else if (colorPair >= 10 && colorPair <= 17)
                    {
                        colorPair = 9;
                    }

                    NCurses.WindowAttributeOn(minefieldScreen, NCurses.ColorPair(colorPair));
                    NCurses.MoveWindowAddString(minefieldScreen, i + 1, j + 1, "#"); // TODO
                    NCurses.WindowAttributeOff(minefieldScreen, NCurses.ColorPair(colorPair));
                }
            }
        }
    }

    public bool OpenOrFlagTile(int key, int[] playerPositionYX)
    {
        int x = playerPositionYX[1], y = playerPositionYX[0];

        if (key == ' ' && !Minefield[y, x].isOpened && !Minefield[y, x].isFlagged)
        {
            Minefield[y, x].isOpened = true;
            if (Minefield[y, x].isMine)
            {
                return true;
            }

            RevealEmptyNeighborTiles(y, x);
            
        }

        else if (key == ' ' && Minefield[y, x].isOpened && !Minefield[y, x].isFlagged)
        {
            return HandleChord(y, x);
        }

        else if ((key == 'f' || key == 'F') && !Minefield[y, x].isOpened)
        {
            Minefield[y, x].isFlagged = !Minefield[y, x].isFlagged;
        }

        return false;
    }

    private void RevealEmptyNeighborTiles(int y, int x)
    {
        if (Minefield[y, x].borderingMineCount != 0)
        {
            return;
        }

        for (int i = y - 1; i < y + 2; i++)
        {
            for (int j = x - 1; j < x + 2; j++)
            {
                if (i < 0 || j < 0 || i >= ScreenHeight || j >= ScreenWidth)
                {
                    continue;
                }

                if (!Minefield[i, j].isOpened && !Minefield[i, j].isFlagged && !Minefield[i, j].isMine)
                {
                    Minefield[i, j].isOpened = true;
                    RevealEmptyNeighborTiles(i, j);
                }
            }
        }
    }

    private bool HandleChord(int y, int x)
    {
        int neighborTilesFlagCount = 0;

        for (int i = y - 1; i < y + 2; i++)
        {
            for (int j = x - 1; j < x + 2; j++)
            {
                if (i < 0 || j < 0 || i >= ScreenHeight || j >= ScreenWidth)
                {
                    continue;
                }
                else if (i == 0 && j == 0)
                {
                    continue;
                }
                else if (Minefield[i, j].isFlagged)
                {
                    neighborTilesFlagCount++;
                }
            }
        }

        if (Minefield[y, x].borderingMineCount != neighborTilesFlagCount)
        {
            return false;
        }

        for (int i = y - 1; i < y + 2; i++)
        {
            for (int j = x - 1; j < x + 2; j++)
            {
                if (i < 0 || j < 0 || i >= ScreenHeight || j >= ScreenWidth)
                {
                    continue;
                }

                if (!Minefield[i, j].isFlagged)
                {
                    Minefield[i, j].isOpened = true;
                    RevealEmptyNeighborTiles(i, j);
                }

                if (!Minefield[i, j].isFlagged && Minefield[i, j].isOpened && Minefield[i, j].isMine)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

class Program
{
    static bool ShouldExit(int key)
    {
        if (key == 'q' || key == 'Q')
        {
            return true;
        }
        return false;
    }

    static int[] UpdatePlayerPosition(int key, int[]playerPositionYX, int minefieldScreenHeight, int minefieldScreenWidth)
    {
        if (key == CursesKey.UP && playerPositionYX[0] > 0)
        {
            playerPositionYX[0]--;
        }
        else if (key == CursesKey.DOWN && playerPositionYX[0] < minefieldScreenHeight - 1)
        {
            playerPositionYX[0]++;
        }
        else if (key == CursesKey.LEFT && playerPositionYX[1] > 0)
        {
            playerPositionYX[1]--;
        }
        else if (key == CursesKey.RIGHT && playerPositionYX[1] < minefieldScreenWidth - 1)
        {
            playerPositionYX[1]++;
        }

        return playerPositionYX;
    }

    static void Main()
    {
        nint screen = NCurses.InitScreen();
        int screenHeight, screenWidth;
        NCurses.GetMaxYX(screen, out screenHeight, out screenWidth);

        if (!NCurses.HasColors())
        {
            NCurses.EndWin();
            Console.WriteLine("ERROR: Your terminal does not support colors");
            Environment.Exit(1);
        }
        NCurses.StartColor();

        // Safe tile number colors
        NCurses.InitPair(1, CursesColor.BLUE, CursesColor.BLACK);
        NCurses.InitPair(2, CursesColor.GREEN, CursesColor.BLACK);
        NCurses.InitPair(3, CursesColor.RED, CursesColor.BLACK);
        NCurses.InitPair(4, CursesColor.MAGENTA, CursesColor.BLACK);
        NCurses.InitPair(5, CursesColor.YELLOW, CursesColor.BLACK);
        NCurses.InitPair(6, CursesColor.CYAN, CursesColor.BLACK);
        NCurses.InitPair(7, CursesColor.WHITE, CursesColor.BLACK);
        NCurses.InitPair(8, CursesColor.WHITE, CursesColor.BLACK);
        
        // Safe tile highlighted
        NCurses.InitPair(9, CursesColor.WHITE, CursesColor.WHITE); // 0
        NCurses.InitPair(10, CursesColor.BLUE, CursesColor.WHITE); // 1 and so on...
        NCurses.InitPair(11, CursesColor.GREEN, CursesColor.WHITE);
        NCurses.InitPair(12, CursesColor.RED, CursesColor.WHITE);
        NCurses.InitPair(13, CursesColor.MAGENTA, CursesColor.WHITE);
        NCurses.InitPair(14, CursesColor.YELLOW, CursesColor.WHITE);
        NCurses.InitPair(15, CursesColor.CYAN, CursesColor.WHITE);
        NCurses.InitPair(16, CursesColor.BLACK, CursesColor.WHITE);
        NCurses.InitPair(17, CursesColor.WHITE, CursesColor.WHITE);

        // Flag and mine colors
        NCurses.InitPair(18, CursesColor.YELLOW, CursesColor.YELLOW);
        NCurses.InitPair(19, CursesColor.RED, CursesColor.RED);

        int minefieldScreenHeight = 20, minefieldScreenWidth = 60, totalMines = 150;
        int[] exclude = { 0, 0 };

        nint minefieldScreen = NCurses.NewWindow(
            minefieldScreenHeight + 2, 
            minefieldScreenWidth + 2, 
            (screenHeight / 2) - (minefieldScreenHeight / 2), 
            (screenWidth / 2) - (minefieldScreenWidth / 2)
        ); 
        NCurses.CBreak();
        NCurses.Keypad(minefieldScreen, true);

        Mines minefield = new(minefieldScreenHeight, minefieldScreenWidth, totalMines);

        int key = 0;
        int[] playerPositionYX = [0, 0];
        bool wrongTileChoosen = false;
        bool gameStarted = false;
        while (!ShouldExit(key) && !wrongTileChoosen)
        {
            NCurses.ClearWindow(minefieldScreen);
            minefield.DisplayMines(minefieldScreen, playerPositionYX, false);
            NCurses.Box(minefieldScreen, '|', '-');
            NCurses.WindowRefresh(minefieldScreen);

            key = NCurses.WindowGetChar(minefieldScreen);

            if (!gameStarted && key == ' ')
            {
                exclude[0] = playerPositionYX[0];
                exclude[1] = playerPositionYX[1];
                minefield.Add_Mines(exclude);
                minefield.CountMineBordering();
                gameStarted = true;
            }

            playerPositionYX = UpdatePlayerPosition(key, playerPositionYX, minefieldScreenHeight, minefieldScreenWidth);
            wrongTileChoosen = minefield.OpenOrFlagTile(key, playerPositionYX);
        }

        if (wrongTileChoosen)
        {
            NCurses.ClearWindow(minefieldScreen);
            minefield.DisplayMines(minefieldScreen, playerPositionYX, true);
            NCurses.Box(minefieldScreen, '|', '-');
            NCurses.WindowRefresh(minefieldScreen);
            Thread.Sleep(2000);
            NCurses.WindowGetChar(minefieldScreen);
        }

        NCurses.EndWin();
    }
}
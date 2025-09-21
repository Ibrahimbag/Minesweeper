using Mindmagma.Curses;

internal class Mines(int MinefieldScreenHeight, int MinefieldScreenWidth, int TotalMines)
{
    public struct Minefield_s
    {
        public bool isMine;
        public int borderingMineCount;
        public bool isOpened;
        public bool isFlagged;
    }

    public Minefield_s[,] Minefield = new Minefield_s[MinefieldScreenHeight, MinefieldScreenWidth];
    private List<int[]> MineLocations = [];

    // Randomly assign mines to the minefield
    public void AddMines(int[] exclude)
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
                randomRow = random.Next(0, MinefieldScreenHeight);
                randomCol = random.Next(0, MinefieldScreenWidth);

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

        MineLocations = randomLocations;
    }

    // Count mines that the tile borders with
    public void CountMineBordering()
    {
        foreach (int[] mineLocation in MineLocations)
        {
            int y = mineLocation[0];
            int x = mineLocation[1];
            for (int i = y - 1; i < y + 2; i++)
            {
                for (int j = x - 1; j < x + 2; j++)
                {
                    if (i >= 0 && j >= 0 && i < MinefieldScreenHeight && j < MinefieldScreenWidth && !Minefield[i, j].isMine)
                    {
                        Minefield[i, j].borderingMineCount++;
                    }
                }
            }
        }
    }

    public void DisplayMines(nint MinefieldScreen, Position PlayerPositionYX, bool game_ended)
    {
        for (int i = 0; i < MinefieldScreenHeight; i++)
        {
            for (int j = 0; j < MinefieldScreenWidth; j++)
            {
                int colorPair = Minefield[i, j].borderingMineCount;
                colorPair = (PlayerPositionYX.Row == i && PlayerPositionYX.Col == j) ? colorPair + 9 : colorPair;

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
                    NCurses.WindowAttributeOn(MinefieldScreen, NCurses.ColorPair(colorPair));
                    colorPair = (PlayerPositionYX.Row == i && PlayerPositionYX.Col == j) ? colorPair - 9 : colorPair;
                    NCurses.MoveWindowAddString(MinefieldScreen, i + 1, j + 1, colorPair.ToString().Replace('0', ' '));
                    colorPair = (PlayerPositionYX.Row == i && PlayerPositionYX.Col == j) ? colorPair + 9 : colorPair;
                    NCurses.WindowAttributeOff(MinefieldScreen, NCurses.ColorPair(colorPair));
                }
                else
                {
                    if (colorPair is >= 1 and <= 8)
                    {
                        colorPair = 0;
                    }
                    else if (colorPair is >= 10 and <= 17)
                    {
                        colorPair = 9;
                    }

                    NCurses.WindowAttributeOn(MinefieldScreen, NCurses.ColorPair(colorPair));
                    NCurses.MoveWindowAddString(MinefieldScreen, i + 1, j + 1, "#");
                    NCurses.WindowAttributeOff(MinefieldScreen, NCurses.ColorPair(colorPair));
                }
            }
        }
    }

    public bool OpenOrFlagTile(int key, Position PlayerPositionYX)
    {
        int x = PlayerPositionYX.Col, y = PlayerPositionYX.Row;

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
                if (i < 0 || j < 0 || i >= MinefieldScreenHeight || j >= MinefieldScreenWidth)
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
                if (i < 0 || j < 0 || i >= MinefieldScreenHeight || j >= MinefieldScreenWidth)
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
                if (i < 0 || j < 0 || i >= MinefieldScreenHeight || j >= MinefieldScreenWidth)
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

    public int CountRemainingMines(int TotalMines)
    {
        int flagCount = 0;
        foreach (Minefield_s m in Minefield)
        {
            if (m.isFlagged)
            {
                flagCount++;
            }
        }

        return TotalMines - flagCount;
    }

    public bool HasWon()
    {
        foreach (Minefield_s tile in Minefield)
        {
            if (!tile.isMine && !tile.isOpened)
            {
                return false;
            }
        }
        return true;
    }

    public void FlagAllMines()
    {
        for (int i = 0; i < MinefieldScreenHeight; i++)
        {
            for (int j = 0; j < MinefieldScreenWidth; j++)
            {
                if (Minefield[i, j].isMine)
                {
                    Minefield[i, j].isFlagged = true;
                }
            }
        }
    }
}

internal class Timer
{
    private static long UnixInitialTime;
    private static long UnixCurrentTime;

    public static void InitTimer()
    {
        DateTime currentTime = DateTime.UtcNow;
        UnixInitialTime = ((DateTimeOffset)currentTime).ToUnixTimeSeconds();
    }

    public static long GetTimeElapsed()
    {
        DateTime currentTime = DateTime.UtcNow;
        UnixCurrentTime = ((DateTimeOffset)currentTime).ToUnixTimeSeconds();
        return Math.Abs(Math.Abs(UnixCurrentTime) - Math.Abs(UnixInitialTime));
    }
}

internal record struct Position(int Row, int Col);

internal class GameContext
{
    // Windows/Screen handles
    public nint Screen { get; set; }
    public nint MinefieldScreen { get; set; }
    public nint TopScreen { get; set; }

    // Screen sizes
    public int ScreenHeight { get; set; } = 0;
    public int ScreenWidth { get; set; } = 0;

    // Minefield settings
    public int MinefieldScreenHeight { get; set; } = 16;
    public int MinefieldScreenWidth { get; set; } = 16;
    public int TotalMines { get; set; } = 40;

    // Game state
    public Mines Minefield { get; set; } = new Mines(0, 0, 0);
    public bool MainMenuShown { get; set; } = false;
    public bool WrongTileChosen { get; set; } = false;
    public bool GameWon { get; set; } = false;

    // Player position
    public Position PlayerPositionYX { get; set; } = new Position(0, 0);
}

internal class Program
{
    private static readonly GameContext context = new();

    private static void Main()
    {
        InitializeGame();
        GameLoop();
        ShowResult();

        if (!NCurses.IsEndWin())
        {
            NCurses.EndWin();
        }
    }

    private static void InitializeGame()
    {
        context.Screen = NCurses.InitScreen();
        NCurses.CBreak();
        NCurses.GetMaxYX(context.Screen, out int screenHeight, out int screenWidth);
        context.ScreenHeight = screenHeight;
        context.ScreenWidth = screenWidth;

        if (NCurses.HasColors())
        {
            InitializeColors();
        }
        else
        {
            NCurses.EndWin();
            Console.WriteLine("ERROR: Your terminal does not support colors");
            Environment.Exit(1);
        }

        if (!context.MainMenuShown)
        {
            string mode = ShowMainMenu();
            SetSizeAndMineCount(mode);
            context.MainMenuShown = true;
        }

        context.MinefieldScreen = NCurses.NewWindow(
            context.MinefieldScreenHeight + 2,
            context.MinefieldScreenWidth + 2,
            (context.ScreenHeight / 2) - (context.MinefieldScreenHeight / 2) - 2,
            (context.ScreenWidth / 2) - (context.MinefieldScreenWidth / 2) - 2
        );
        NCurses.Keypad(context.MinefieldScreen, true);

        context.TopScreen = NCurses.NewWindow(1, context.ScreenWidth, 0, 0);

        context.Minefield = new(context.MinefieldScreenHeight, context.MinefieldScreenWidth, context.TotalMines);
    }

    private static void InitializeColors()
    {
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
    }

    private static string ShowMainMenu()
    {
        // Ascii art created from here: https://www.asciiart.eu/text-to-ascii-art         
        string[] logo =
        [
            "           \\  | _)",
            "          |\\/ |  |    \\    -_)    ",
            "  __|    _|  _| _| _| _| \\___|     ",
            "\\__ \\ \\ \\  \\ /  -_)   -_)  _ \\   -_)   _|",
            "____/  \\_/\\_/ \\___| \\___| .__/ \\___| _|",
            "                         _|"
        ];

        int logoHeight = logo.Length, logoWidth = logo[3].Length;

        nint menuScreen = NCurses.NewWindow(context.ScreenHeight, context.ScreenWidth, 0, 0);

        for (int i = 0; i < logoHeight; i++)
        {
            NCurses.MoveWindowAddString(menuScreen, i + 2, (context.ScreenWidth / 2) - (logoWidth / 2), logo[i]);
        }

        NCurses.WindowRefresh(menuScreen);

        nint difficultyScreen = NCurses.NewWindow(3, context.ScreenWidth, context.ScreenHeight - 5, 0);
        NCurses.Keypad(difficultyScreen, true);

        string[] modes = ["<- Easy ->", "<- Normal ->", "<- Hard ->", "<- Custom ->"];
        int currentModeIndex = 0;

        int key;
        do
        {
            NCurses.ClearWindow(difficultyScreen);
            string notice = "Choose your difficulty using left or right arrow key";
            NCurses.MoveWindowAddString(difficultyScreen, 0, (context.ScreenWidth / 2) - (notice.Length / 2), notice);
            NCurses.MoveWindowAddString(difficultyScreen, 2, (context.ScreenWidth / 2) - (modes[currentModeIndex].Length / 2), modes[currentModeIndex]);
            NCurses.WindowRefresh(difficultyScreen);

            key = NCurses.WindowGetChar(difficultyScreen);
            if (key == CursesKey.RIGHT && currentModeIndex < modes.Length - 1)
            {
                currentModeIndex++;
            }
            else if (key == CursesKey.LEFT && currentModeIndex > 0)
            {
                currentModeIndex--;
            }
        }
        while (key != 10);

        NCurses.ClearWindow(menuScreen);
        NCurses.ClearWindow(difficultyScreen);
        NCurses.WindowRefresh(menuScreen);
        NCurses.WindowRefresh(difficultyScreen);

        return modes[currentModeIndex];
    }

    private static void SetSizeAndMineCount(string mode)
    {
        int[] easyMode = [9, 9, 10];
        int[] normalMode = [16, 16, 40];
        int[] hardMode = [16, 30, 99];

        if (mode.Contains("Easy"))
        {
            context.MinefieldScreenHeight = easyMode[0];
            context.MinefieldScreenWidth = easyMode[1];
            context.TotalMines = easyMode[2];
        }
        else if (mode.Contains("Normal"))
        {
            context.MinefieldScreenHeight = normalMode[0];
            context.MinefieldScreenWidth = normalMode[1];
            context.TotalMines = normalMode[2];
        }
        else if (mode.Contains("Hard"))
        {
            context.MinefieldScreenHeight = hardMode[0];
            context.MinefieldScreenWidth = hardMode[1];
            context.TotalMines = hardMode[2];
        }
        else
        {
            ShowCustomMenu();
        }

        if (context.MinefieldScreenHeight > context.ScreenHeight - 8)
        {
            context.MinefieldScreenHeight = context.ScreenHeight - 8;
        }
        if (context.MinefieldScreenWidth > context.ScreenWidth - 3)
        {
            context.MinefieldScreenWidth = context.ScreenWidth - 3;
        }

        if (context.MinefieldScreenHeight * context.MinefieldScreenWidth <= context.TotalMines)
        {
            context.TotalMines = context.MinefieldScreenHeight * context.MinefieldScreenWidth / 2;
        }
    }

    private static void ShowCustomMenu()
    {
        nint customScreen = NCurses.NewWindow(1, context.ScreenWidth, context.ScreenHeight / 2, 0);

        List<int[]> customInputs = [];
        int[] buffer;
        int buffer_size;

        string[] settings = ["Height: ", "Width: ", "Mines: "];

        foreach (int i in Enumerable.Range(1, 3))
        {
            NCurses.ClearWindow(customScreen);
            buffer = new int[3];
            buffer_size = 0;

            while (true)
            {
                NCurses.MoveWindowAddString(customScreen, 0, (context.ScreenWidth / 2) - settings[i - 1].Length, settings[i - 1]);
                int key = NCurses.MoveWindowGetChar(customScreen, 0, (context.ScreenWidth / 2) + buffer_size);

                if (key == 10)
                {
                    break;
                }
                else if (key == CursesKey.BACKSPACE && buffer_size > 0)
                {
                    buffer[buffer_size - 1] = 0;
                    buffer_size--;
                    NCurses.MoveWindowAddString(customScreen, 0, (context.ScreenWidth / 2) + buffer_size, " ");
                }
                else if (char.IsDigit((char)key))
                {
                    try
                    {
                        buffer[buffer_size] = key;
                    }
                    catch (IndexOutOfRangeException)
                    {
                        break;
                    }

                    buffer_size++;
                }
                else
                {
                    NCurses.MoveWindowAddString(customScreen, 0, (context.ScreenWidth / 2) + buffer_size, " ");
                }
            }

            customInputs.Add(buffer);
        }

        string[] result = new string[customInputs.Count];
        for (int i = 0; i < customInputs.Count; i++)
        {
            string total = "";
            int[] input = customInputs[i];

            for (int j = 0; j < input.Length; j++)
            {
                if (input[j] != 0)
                {
                    total += (input[j] + 1 - '1').ToString();
                }
            }

            result[i] = total;
        }

        _ = int.TryParse(result[0], out int height);
        _ = int.TryParse(result[1], out int width);
        _ = int.TryParse(result[2], out int mines);
        context.MinefieldScreenHeight = height;
        context.MinefieldScreenWidth = width;
        context.TotalMines = mines;

        NCurses.ClearWindow(customScreen);
        NCurses.WindowRefresh(customScreen);
    }

    private static void GameLoop()
    {
        int key = 0;
        Position exclude;
        bool gameStarted = false;

        while (!ShouldExit(key) && !context.WrongTileChosen && !(context.GameWon = context.Minefield.HasWon()))
        {
            NCurses.ClearWindow(context.MinefieldScreen);
            context.Minefield.DisplayMines(context.MinefieldScreen, context.PlayerPositionYX, false);

            int colorPair = 0;
            Mines.Minefield_s focusedMine = context.Minefield.Minefield[context.PlayerPositionYX.Row, context.PlayerPositionYX.Col];
            if (focusedMine.isOpened)
            {
                colorPair = context.Minefield.Minefield[context.PlayerPositionYX.Row, context.PlayerPositionYX.Col].borderingMineCount;
            }
            else if (focusedMine.isFlagged)
            {
                colorPair = 5;
            }
            NCurses.WindowAttributeOn(context.MinefieldScreen, NCurses.ColorPair(colorPair));
            NCurses.Box(context.MinefieldScreen, '|', '-');
            NCurses.WindowAttributeOff(context.MinefieldScreen, NCurses.ColorPair(colorPair));

            NCurses.WindowRefresh(context.MinefieldScreen);

            DisplayRemainingMines(context.TopScreen, context.Minefield);

            key = NCurses.WindowGetChar(context.MinefieldScreen);

            if (!gameStarted && key == ' ')
            {
                exclude = context.PlayerPositionYX;
                context.Minefield.AddMines([exclude.Row, exclude.Col]);
                context.Minefield.CountMineBordering();
                Timer.InitTimer();
                gameStarted = true;
            }

            context.PlayerPositionYX = UpdatePlayerPosition(key);
            context.WrongTileChosen = context.Minefield.OpenOrFlagTile(key, context.PlayerPositionYX);
        }
    }

    private static bool ShouldExit(int key)
    {
        return key is 'q' or 'Q';
    }

    private static void DisplayRemainingMines(nint TopScreen, Mines Minefield)
    {
        int remainingMineCount = Minefield.CountRemainingMines(context.TotalMines);

        NCurses.WindowAttributeOn(TopScreen, CursesAttribute.REVERSE);
        NCurses.ClearWindow(TopScreen);
        NCurses.MoveWindowAddString(TopScreen, 0, 0, $"Remaning mines: {remainingMineCount}");
        NCurses.WindowRefresh(TopScreen);
        NCurses.WindowAttributeOff(TopScreen, CursesAttribute.REVERSE);
    }

    private static Position UpdatePlayerPosition(int key)
    {
        int row = context.PlayerPositionYX.Row;
        int col = context.PlayerPositionYX.Col;

        if (key == CursesKey.UP && row > 0)
        {
            row--;
        }
        else if (key == CursesKey.DOWN && row < context.MinefieldScreenHeight - 1)
        {
            row++;
        }
        else if (key == CursesKey.LEFT && col > 0)
        {
            col--;
        }
        else if (key == CursesKey.RIGHT && col < context.MinefieldScreenWidth - 1)
        {
            col++;
        }

        return new Position(row, col);
    }

    private static void ShowResult()
    {
        if (context.WrongTileChosen || context.GameWon)
        {
            NCurses.ClearWindow(context.MinefieldScreen);

            if (context.GameWon)
            {
                context.Minefield.FlagAllMines();
            }

            context.Minefield.DisplayMines(context.MinefieldScreen, context.PlayerPositionYX, context.WrongTileChosen);
            int colorPair = context.GameWon ? 2 : 3;
            string gameResult = context.GameWon ? $"You win in {Timer.GetTimeElapsed()} seconds!" : "You lose!";
            gameResult += " Press R to restart, Q to quit.";

            NCurses.WindowAttributeOn(context.MinefieldScreen, NCurses.ColorPair(colorPair));
            NCurses.Box(context.MinefieldScreen, '|', '-');
            NCurses.WindowAttributeOff(context.MinefieldScreen, NCurses.ColorPair(colorPair));

            NCurses.MoveAddString(
                (context.ScreenHeight / 2) - (context.MinefieldScreenHeight / 2) + context.MinefieldScreenHeight + 3,
                (context.ScreenWidth / 2) - (gameResult.Length / 2),
                gameResult
            );
            NCurses.Refresh();

            NCurses.WindowRefresh(context.MinefieldScreen);

            int ch;
            do
            {
                ch = NCurses.WindowGetChar(context.MinefieldScreen);
            }
            while (ch is not 'r' and not 'R' and not 'q' and not 'Q');

            if (ch is 'r' or 'R')
            {
                context.WrongTileChosen = false;
                context.GameWon = false;

                NCurses.Clear();
                NCurses.Refresh();
                NCurses.EndWin();
                Main();
            }
        }
    }
}
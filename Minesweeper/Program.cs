using Mindmagma.Curses;
using Newtonsoft.Json;
using System.Diagnostics;

/// <summary>
/// Identifiers for color pairs used by the UI. Values map to color pair indices
/// initialized in <see cref="CursesUI.InitializeColors"/>.
/// </summary>
public enum ColorIds
{
    None,
    Blue,
    Green,
    Red,
    Magenta,
    Yellow,
    Cyan,
    White,
    White2,

    NoneHighlight,
    BlueHighlight,
    GreenHighlight,
    RedHighlight,
    MagentaHighlight,
    YellowHighlight,
    CyanHighlight,
    BlackHighlight,
    WhiteHighlight,

    Flag,
    Mine,
};

/// <summary>
/// Represents the Minesweeper minefield and related game logic.
/// This class encapsulates the tile grid, mine placement and reveal logic.
/// </summary>
/// <param name="MinefieldScreenHeight">Height of the minefield (rows).</param>
/// <param name="MinefieldScreenWidth">Width of the minefield (columns).</param>
/// <param name="TotalMines">Total mines to place in the field.</param>
internal class Mines(int MinefieldScreenHeight, int MinefieldScreenWidth, int TotalMines)
{
    /// <summary>
    /// Represents a single tile on the minefield.
    /// Fields are public for simplicity and manipulated directly by game logic.
    /// </summary>
    public struct Tile
    {
        public bool isMine;
        public int borderingMineCount;
        public bool isOpened;
        public bool isFlagged;
    }

    /// <summary>
    /// 2D array of tiles indexed by [row, column].
    /// </summary>
    public Tile[,] Minefield = new Tile[MinefieldScreenHeight, MinefieldScreenWidth];

    // Stores mine coordinates as int[] { row, column }.
    private List<int[]> MineLocations = [];

    /// <summary>
    /// Randomly assign mines to the minefield excluding the provided coordinate.
    /// Ensures no duplicate mine locations and excludes the first clicked tile when called on first move.
    /// </summary>
    /// <param name="exclude">A 2-element int array {row, col} to exclude from mine placement.</param>
    public void AddMines(int[] exclude)
    {
        Random random = new();

        // Collect unique mine coordinates
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

                // Prevent placing a mine on the excluded tile (first opened tile)
                if (exclude.SequenceEqual([randomRow, randomCol]))
                {
                    isDuplicateOrExclude = true;
                    continue;
                }

                // Ensure uniqueness among already selected random locations
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

        // Mark selected tiles as mines
        foreach (int[] randomLocation in randomLocations)
        {
            Minefield[randomLocation[0], randomLocation[1]].isMine = true;
        }

        MineLocations = randomLocations;
    }

    /// <summary>
    /// Populate each tile's borderingMineCount by iterating over known mine locations.
    /// Only increments counts for non-mine neighbor tiles inside bounds.
    /// </summary>
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
                    // Bounds-check and avoid incrementing the mine tile itself
                    if (i >= 0 && j >= 0 && i < MinefieldScreenHeight && j < MinefieldScreenWidth && !Minefield[i, j].isMine)
                    {
                        Minefield[i, j].borderingMineCount++;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Open or toggle-flag the tile under the player's position based on input key.
    /// Returns true when a mine is opened (game over).
    /// </summary>
    /// <param name="key">Input key code (space for open/chord, 'f' to flag).</param>
    /// <param name="PlayerPositionYX">Player cursor position as (Row, Col).</param>
    /// <returns>True if opening a tile caused a mine to be revealed.</returns>
    public bool OpenOrFlagTile(int key, Position PlayerPositionYX)
    {
        int x = PlayerPositionYX.Col, y = PlayerPositionYX.Row;

        if (key == ' ' && !Minefield[y, x].isOpened && !Minefield[y, x].isFlagged)
        {
            // Open a closed, unflagged tile
            Minefield[y, x].isOpened = true;
            if (Minefield[y, x].isMine)
            {
                // Opened a mine -> game over
                return true;
            }

            // If the tile is empty (0 bordering mines), reveal its neighbors recursively
            RevealEmptyNeighborTiles(y, x);
        }
        else if (key == ' ' && Minefield[y, x].isOpened && !Minefield[y, x].isFlagged)
        {
            // Chording behavior: attempt to open surrounding tiles if flags match borderingMineCount
            return HandleChord(y, x);
        }
        else if ((key == 'f' || key == 'F') && !Minefield[y, x].isOpened)
        {
            // Toggle flag on closed tile
            Minefield[y, x].isFlagged = !Minefield[y, x].isFlagged;
        }

        return false;
    }

    /// <summary>
    /// Recursively reveal adjacent tiles when a tile with zero bordering mines is opened.
    /// Stops at map edges and when encountering mines or flagged/open tiles.
    /// </summary>
    private void RevealEmptyNeighborTiles(int y, int x)
    {
        if (Minefield[y, x].borderingMineCount != 0)
        {
            // Tile is not empty; no recursive expansion required.
            return;
        }

        for (int i = y - 1; i < y + 2; i++)
        {
            for (int j = x - 1; j < x + 2; j++)
            {
                // Skip out-of-bounds neighbors
                if (i < 0 || j < 0 || i >= MinefieldScreenHeight || j >= MinefieldScreenWidth)
                {
                    continue;
                }

                // Reveal neighbor if it's a closed, non-flagged, non-mine tile and recurse
                if (!Minefield[i, j].isOpened && !Minefield[i, j].isFlagged && !Minefield[i, j].isMine)
                {
                    Minefield[i, j].isOpened = true;
                    RevealEmptyNeighborTiles(i, j);
                }
            }
        }
    }

    /// <summary>
    /// Handle the "chord" operation: if the number of flagged neighbors equals this tile's borderingMineCount,
    /// open all non-flagged neighbors. Returns true if a mine gets inadvertently opened.
    /// </summary>
    /// <returns> True if mine opened during chord operation (game over)</returns>
    private bool HandleChord(int y, int x)
    {
        int neighborTilesFlagCount = 0;

        // Count flagged neighbors
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
                    // This condition appears intended to skip the center tile; but it's comparing indices to zero.
                    // Minimal assumption: It attempts to skip the center tile; this is harmless if center isn't (0,0).
                    continue;
                }
                else if (Minefield[i, j].isFlagged)
                {
                    neighborTilesFlagCount++;
                }
            }
        }

        // If the flagged count does not match the tile's number, chord is not allowed
        if (Minefield[y, x].borderingMineCount != neighborTilesFlagCount)
        {
            return false;
        }

        // Open surrounding tiles that are not flagged
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

                // If a mine was opened during chord, report game over
                if (!Minefield[i, j].isFlagged && Minefield[i, j].isOpened && Minefield[i, j].isMine)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Count how many mines remain (TotalMines - flagged tiles).
    /// </summary>
    /// <param name="TotalMines">Total mine count from game settings.</param>
    /// <returns>Remaining mine count to display to the player.</returns>
    public int CountRemainingMines(int TotalMines)
    {
        int flagCount = 0;
        foreach (Tile m in Minefield)
        {
            if (m.isFlagged)
            {
                flagCount++;
            }
        }

        return TotalMines - flagCount;
    }

    /// <summary>
    /// Determine if the player has opened all non-mine tiles.
    /// </summary>
    /// <returns>True if every non-mine tile is opened; otherwise false.</returns>
    public bool HasWon()
    {
        foreach (Tile tile in Minefield)
        {
            if (!tile.isMine && !tile.isOpened)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Mark every mine in the field as flagged. Used when the game is won to visually mark mines.
    /// </summary>
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

/// <summary>
/// Simple timer used to measure elapsed game time. Uses UTC to avoid local time issues.
/// </summary>
internal static class Timer
{
    private static DateTime start;
    public static long SavedTimeElapsed = 0;

    /// <summary>
    /// Start or restart the timer.
    /// </summary>
    public static void InitTimer()
    {
        start = DateTime.UtcNow;
    }

    /// <summary>
    /// Get total elapsed seconds since timer start plus any saved elapsed time.
    /// </summary>
    /// <returns>Elapsed time in seconds.</returns>
    public static long GetTimeElapsed()
    {
        return (long)(DateTime.UtcNow - start).TotalSeconds + SavedTimeElapsed;
    }
}

/// <summary>
/// Immutable struct representing a 2D position as (Row, Col).
/// </summary>
/// <param name="Row">Row index (Y coordinate).</param>
/// <param name="Col">Column index (X coordinate).</param>
internal record struct Position(int Row, int Col);

/// <summary>
/// Holds global game state and window handles used by the UI.
/// </summary>
internal class GameContext
{
    /// <summary>
    /// Standard screen handle (stdscr) returned by NCurses.InitScreen.
    /// </summary>
    public nint Screen { get; set; }

    /// <summary>
    /// Window handle that contains the minefield.
    /// </summary>
    public nint MinefieldScreen { get; set; }

    /// <summary>
    /// Small top bar window used to display remaining mines etc.
    /// </summary>
    public nint TopScreen { get; set; }

    // Terminal/screen dimensions
    public int ScreenHeight { get; set; } = 0;
    public int ScreenWidth { get; set; } = 0;

    // Minefield settings (defaults)
    public int MinefieldScreenHeight { get; set; } = 16;
    public int MinefieldScreenWidth { get; set; } = 16;
    public int TotalMines { get; set; } = 40;

    // Game state flags
    public Mines Minefield { get; set; } = new Mines(0, 0, 0);
    public bool MainMenuShown { get; set; } = false;
    public bool WrongTileChosen { get; set; } = false;
    public bool GameWon { get; set; } = false;

    // Player cursor position within the minefield grid
    public Position PlayerPositionYX { get; set; } = new Position(0, 0);
}

/// <summary>
/// Handles all UI drawing operations using the NCurses wrapper.
/// Methods here operate on windows stored in <see cref="GameContext"/>.
/// </summary>
internal class CursesUI
{
    /// <summary>
    /// Draw the minefield contents into the minefield window.
    /// This method chooses color pairs depending on tile state and whether the game has ended.
    /// </summary>
    /// <param name="context">Current game context and window handles.</param>
    /// <param name="gameEnded">True when the game has ended (used to reveal mines).</param>
    public static void DisplayMines(GameContext context, bool gameEnded)
    {
        for (int i = 0; i < context.MinefieldScreenHeight; i++)
        {
            for (int j = 0; j < context.MinefieldScreenWidth; j++)
            {
                int colorPair = context.Minefield.Minefield[i, j].borderingMineCount;
                // Add highlight offset if the player cursor is on this tile
                colorPair = (context.PlayerPositionYX.Row == i && context.PlayerPositionYX.Col == j) ? colorPair + (int)ColorIds.NoneHighlight : colorPair;

                if (context.Minefield.Minefield[i, j].isFlagged && !context.Minefield.Minefield[i, j].isOpened)
                {
                    // Flags have their own dedicated color pair
                    colorPair = (int)ColorIds.Flag;
                }
                else if (context.Minefield.Minefield[i, j].isMine && gameEnded)
                {
                    // Reveal mines only when the game ended
                    colorPair = (int)ColorIds.Mine;
                }

                if (context.Minefield.Minefield[i, j].isOpened)
                {
                    NCurses.WindowAttributeOn(context.MinefieldScreen, NCurses.ColorPair(colorPair));
                    // If highlighted, remove the highlight for the displayed character itself (visual choice)
                    colorPair = (context.PlayerPositionYX.Row == i && context.PlayerPositionYX.Col == j) ? colorPair - (int)ColorIds.NoneHighlight : colorPair;
                    // Display numeric tile value (or blank for 0)
                    NCurses.MoveWindowAddString(context.MinefieldScreen, i + 1, j + 1, colorPair.ToString().Replace('0', ' '));
                    colorPair = (context.PlayerPositionYX.Row == i && context.PlayerPositionYX.Col == j) ? colorPair + (int)ColorIds.NoneHighlight : colorPair;
                    NCurses.WindowAttributeOff(context.MinefieldScreen, NCurses.ColorPair(colorPair));
                }
                else
                {
                    // Normalize colorPair into non-highlight variants for closed tiles
                    if (colorPair is >= (int)ColorIds.Blue and <= (int)ColorIds.White2)
                    {
                        colorPair = (int)ColorIds.None;
                    }
                    else if (colorPair is >= (int)ColorIds.BlueHighlight and <= (int)ColorIds.WhiteHighlight)
                    {
                        colorPair = (int)ColorIds.NoneHighlight;
                    }

                    NCurses.WindowAttributeOn(context.MinefieldScreen, NCurses.ColorPair(colorPair));
                    // Draw closed tile marker
                    NCurses.MoveWindowAddString(context.MinefieldScreen, i + 1, j + 1, "#");
                    NCurses.WindowAttributeOff(context.MinefieldScreen, NCurses.ColorPair(colorPair));
                }
            }
        }
    }

    /// <summary>
    /// Initialize all color pairs used by the application.
    /// This maps application-level <see cref="ColorIds"/> to concrete foreground/background colors.
    /// </summary>
    public static void InitializeColors()
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

        // Safe tile highlighted variants (white background)
        NCurses.InitPair(9, CursesColor.WHITE, CursesColor.WHITE); // 0
        NCurses.InitPair(10, CursesColor.BLUE, CursesColor.WHITE); // 1 and so on...
        NCurses.InitPair(11, CursesColor.GREEN, CursesColor.WHITE);
        NCurses.InitPair(12, CursesColor.RED, CursesColor.WHITE);
        NCurses.InitPair(13, CursesColor.MAGENTA, CursesColor.WHITE);
        NCurses.InitPair(14, CursesColor.YELLOW, CursesColor.WHITE);
        NCurses.InitPair(15, CursesColor.CYAN, CursesColor.WHITE);
        NCurses.InitPair(16, CursesColor.BLACK, CursesColor.WHITE);
        NCurses.InitPair(17, CursesColor.WHITE, CursesColor.WHITE);

        // Flag and mine colors (distinctive)
        NCurses.InitPair(18, CursesColor.YELLOW, CursesColor.YELLOW);
        NCurses.InitPair(19, CursesColor.RED, CursesColor.RED);
    }

    /// <summary>
    /// Draws the main menu and handles user selection between new or continued games and difficulty choice.
    /// </summary>
    /// <param name="context">Game context containing terminal dimensions.</param>
    /// <returns>A mode string describing the chosen option (e.g. "Load", "<- Easy ->", etc.).</returns>
    public static string ShowMainMenu(GameContext context)
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

        // Create a full-screen menu window
        nint menuScreen = NCurses.NewWindow(context.ScreenHeight, context.ScreenWidth, 0, 0);

        for (int i = 0; i < logoHeight; i++)
        {
            // Center logo horizontally
            NCurses.MoveWindowAddString(menuScreen, i + 2, (context.ScreenWidth / 2) - (logoWidth / 2), logo[i]);
        }

        NCurses.WindowRefresh(menuScreen);

        // Small window for choices at the bottom area
        nint newGameScreen = NCurses.NewWindow(3, context.ScreenWidth, context.ScreenHeight - 5, 0);
        NCurses.Keypad(newGameScreen, true);
        string[] choices = ["New Game", "Continue"];

        int currentIndex = 0, key;

        do
        {
            NCurses.ClearWindow(newGameScreen);

            string selectedChoice = string.Format("> {0} <", choices[currentIndex]);

            int horizontalOffset = 15;

            // If save file exists, show the "Continue" choice
            if (File.Exists(SaveManager.FileName))
            {
                NCurses.MoveWindowAddString(
                    newGameScreen,
                    2,
                    (context.ScreenWidth / 2) - (choices[1].Length / 2) + horizontalOffset,
                    currentIndex == 1 ? selectedChoice : choices[1]
                );
            }
            else
            {
                horizontalOffset = 0;
            }

            NCurses.MoveWindowAddString(
                newGameScreen,
                2,
                (context.ScreenWidth / 2) - (choices[0].Length / 2) - horizontalOffset,
                currentIndex == 0 ? selectedChoice : choices[0]
            );

            key = NCurses.WindowGetChar(newGameScreen);
            if (key == CursesKey.RIGHT && currentIndex < choices.Length - 1)
            {
                currentIndex++;
            }
            else if (key == CursesKey.LEFT && currentIndex > 0)
            {
                currentIndex--;
            }

            NCurses.WindowRefresh(newGameScreen);
        }
        while (key != 10); // Enter to confirm selection

        NCurses.ClearWindow(newGameScreen);
        NCurses.WindowRefresh(newGameScreen);

        if (currentIndex == 1)
        {
            NCurses.ClearWindow(menuScreen);
            NCurses.WindowRefresh(menuScreen);
            return "Load";
        }

        // Difficulty selection window (reused)
        nint difficultyScreen = NCurses.NewWindow(3, context.ScreenWidth, context.ScreenHeight - 5, 0);
        NCurses.Keypad(difficultyScreen, true);
        string[] modes = ["<- Easy ->", "<- Normal ->", "<- Hard ->", "<- Custom ->"];

        currentIndex = 0;

        do
        {
            NCurses.ClearWindow(difficultyScreen);
            string notice = "Choose your difficulty using left or right arrow key";
            NCurses.MoveWindowAddString(difficultyScreen, 0, (context.ScreenWidth / 2) - (notice.Length / 2), notice);
            NCurses.MoveWindowAddString(difficultyScreen, 2, (context.ScreenWidth / 2) - (modes[currentIndex].Length / 2), modes[currentIndex]);
            NCurses.WindowRefresh(difficultyScreen);

            key = NCurses.WindowGetChar(difficultyScreen);
            if (key == CursesKey.RIGHT && currentIndex < modes.Length - 1)
            {
                currentIndex++;
            }
            else if (key == CursesKey.LEFT && currentIndex > 0)
            {
                currentIndex--;
            }
        }
        while (key != 10);

        NCurses.ClearWindow(menuScreen);
        NCurses.ClearWindow(difficultyScreen);
        NCurses.WindowRefresh(menuScreen);
        NCurses.WindowRefresh(difficultyScreen);

        return modes[currentIndex];
    }

    /// <summary>
    /// Show a small custom configuration menu to accept numeric input for height, width and mines.
    /// The method reads digits from the user and builds integers from the entered digits.
    /// </summary>
    /// <param name="context">Game context used for width and to place the input window.</param>
    public static void ShowCustomMenu(GameContext context)
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
                    // Enter: finish current input
                    break;
                }
                else if (key == CursesKey.BACKSPACE && buffer_size > 0)
                {
                    // Erase last digit from input buffer and UI
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
                        // Input too long for buffer -> stop accepting further digits
                        break;
                    }

                    buffer_size++;
                }
                else
                {
                    // Non-digit input: ignore and clear the position
                    NCurses.MoveWindowAddString(customScreen, 0, (context.ScreenWidth / 2) + buffer_size, " ");
                }
            }

            customInputs.Add(buffer);
        }

        // Convert collected digit buffers into strings then integers
        string[] result = new string[customInputs.Count];
        for (int i = 0; i < customInputs.Count; i++)
        {
            string total = "";
            int[] input = customInputs[i];

            for (int j = 0; j < input.Length; j++)
            {
                if (input[j] != 0)
                {
                    // Convert from stored key codes to character value; minimal assumption applied.
                    total += (input[j] + 1 - '1').ToString();
                }
            }

            result[i] = total;
        }

        _ = int.TryParse(result[0], out int height);
        _ = int.TryParse(result[1], out int width);
        _ = int.TryParse(result[2], out int mines);

        // Apply custom settings to context (may be 0 if parse failed)
        context.MinefieldScreenHeight = height;
        context.MinefieldScreenWidth = width;
        context.TotalMines = mines;

        NCurses.ClearWindow(customScreen);
        NCurses.WindowRefresh(customScreen);
    }

    /// <summary>
    /// Update the top bar to show the number of remaining mines (unflagged).
    /// </summary>
    /// <param name="context">Context containing top window and minefield state.</param>
    public static void DisplayRemainingMines(GameContext context)
    {
        int remainingMineCount = context.Minefield.CountRemainingMines(context.TotalMines);

        NCurses.WindowAttributeOn(context.TopScreen, CursesAttribute.REVERSE);
        NCurses.ClearWindow(context.TopScreen);
        NCurses.MoveWindowAddString(context.TopScreen, 0, 0, $"Remaning mines: {remainingMineCount}");
        NCurses.WindowRefresh(context.TopScreen);
        NCurses.WindowAttributeOff(context.TopScreen, CursesAttribute.REVERSE);
    }

    /// <summary>
    /// Display the final game result (win/lose) and prompt for restart/quit.
    /// The method redraws the minefield then draws a centered message.
    /// </summary>
    /// <remarks>
    /// Note: This method uses stdscr MoveAddString when centering across the entire screen.
    /// Care must be taken that context.ScreenWidth/Height are set correctly to avoid writing
    /// outside the terminal bounds.
    /// </remarks>
    public static void ShowResult(GameContext context)
    {
        if (context.WrongTileChosen || context.GameWon)
        {
            NCurses.ClearWindow(context.MinefieldScreen);

            if (context.GameWon)
            {
                context.Minefield.FlagAllMines();
            }

            // Draw minefield in final state (revealed or flagged)
            DisplayMines(context, context.WrongTileChosen);
            int colorPair = context.GameWon ? (int)ColorIds.Green : (int)ColorIds.Red;
            string gameResult = context.GameWon ? $"You win in {Timer.GetTimeElapsed()} seconds!" : "You lose!";
            gameResult += " Press R to restart, Q to quit.";

            CursesUI.DrawBox(context, colorPair);

            // Compute a centered-ish position for the result message.
            // Be aware this can produce out-of-bounds coordinates if terminal is too small.
            NCurses.MoveAddString(
                (context.ScreenHeight / 2) - (context.MinefieldScreenHeight / 2) + context.MinefieldScreenHeight + 3,
                (context.ScreenWidth / 2) - (gameResult.Length / 2),
                gameResult
            );
            NCurses.Refresh();

            NCurses.WindowRefresh(context.MinefieldScreen);
        }
    }

    /// <summary>
    /// Draw the box (border) around the minefield window using the provided color pair (color of tile focused on).
    /// </summary>
    /// <param name="context">Context containing the minefield window handle.</param>
    /// <param name="colorPair">Color pair index to use for the border.</param>
    public static void DrawBox(GameContext context, int colorPair)
    {
        NCurses.WindowAttributeOn(context.MinefieldScreen, NCurses.ColorPair(colorPair));
        NCurses.Box(context.MinefieldScreen, '|', '-');
        NCurses.WindowAttributeOff(context.MinefieldScreen, NCurses.ColorPair(colorPair));
    }
}

/// <summary>
/// Save and load game state to a JSON file on disk.
/// </summary>
internal static class SaveManager
{
    public static readonly string FileName = "save.json";

    /// <summary>
    /// Serializable container for persistent game state.
    /// </summary>
    private class SaveData
    {
        // Minefield settings
        public int MinefieldScreenHeight { get; set; }
        public int MinefieldScreenWidth { get; set; }
        public int TotalMines { get; set; }

        // Game state
        public Mines.Tile[,] Minefield { get; set; }
        public bool MainMenuShown { get; set; }
        public bool WrongTileChosen { get; set; }
        public bool GameWon { get; set; }

        // Player position
        public Position PlayerPositionYX { get; set; }

        public long SavedTimeElapsed { get; set; }
    }

    /// <summary>
    /// Persist the current game context to disk as JSON.
    /// </summary>
    /// <param name="context">Game context to serialize.</param>
    public static void SaveGame(GameContext context)
    {
        SaveData data = new()
        {
            MinefieldScreenHeight = context.MinefieldScreenHeight,
            MinefieldScreenWidth = context.MinefieldScreenWidth,
            TotalMines = context.TotalMines,
            Minefield = context.Minefield.Minefield,
            MainMenuShown = context.MainMenuShown,
            WrongTileChosen = context.WrongTileChosen,
            GameWon = context.GameWon,
            PlayerPositionYX = context.PlayerPositionYX,
            SavedTimeElapsed = Timer.GetTimeElapsed()
        };

        string jsonString = JsonConvert.SerializeObject(data);

        File.WriteAllText(FileName, jsonString);
    }

    /// <summary>
    /// Load saved game data into the provided context. Throws if the file is missing or corrupted.
    /// </summary>
    /// <param name="context">Game context to populate with saved data.</param>
    public static void LoadGame(GameContext context)
    {
        if (!File.Exists(FileName))
        {
            throw new FileNotFoundException("Save file not found.", FileName);
        }

        string jsonString = File.ReadAllText(FileName);
        SaveData data = JsonConvert.DeserializeObject<SaveData>(jsonString) 
            ?? throw new InvalidDataException("Save file is corrupted or contains invalid data.");

        // Map deserialized values back to the runtime context
        context.MinefieldScreenHeight = data.MinefieldScreenHeight;
        context.MinefieldScreenWidth = data.MinefieldScreenWidth;
        context.TotalMines = data.TotalMines;
        context.Minefield = new(context.MinefieldScreenHeight, context.MinefieldScreenWidth, context.TotalMines);
        context.Minefield.Minefield = data.Minefield;
        context.MainMenuShown = data.MainMenuShown;
        context.WrongTileChosen = data.WrongTileChosen;
        context.GameWon = data.GameWon;
        context.PlayerPositionYX = data.PlayerPositionYX;
        Timer.SavedTimeElapsed = data.SavedTimeElapsed;

        // Reset the running timer after loading (keeps saved elapsed time in Timer.SavedTimeElapsed)
        Timer.InitTimer();
    }
}

/// <summary>
/// Main program orchestrating initialization, game loop and cleanup.
/// </summary>
internal class Program
{
    private static readonly GameContext context = new();

    /// <summary>
    /// Program entry point: initializes, runs the game loop and handles restart/quit flow.
    /// </summary>
    public static void Main()
    {
        int key;
        do
        {
            string mode = InitializeGame();
            key = GameLoop(mode);
            CursesUI.ShowResult(context);
        }
        while (CheckForRestart(key));

        if (!NCurses.IsEndWin())
        {
            NCurses.EndWin();
        }
    }

    /// <summary>
    /// Initialize NCurses, colors and windows, and optionally load a saved game or show menus.
    /// This sets up context.ScreenWidth/Height and creates the Top and Minefield windows.
    /// </summary>
    /// <returns>The selected mode string from the main menu or empty string if already shown.</returns>
    private static string InitializeGame()
    {
        context.Screen = NCurses.InitScreen();
        NCurses.CBreak();
        NCurses.GetMaxYX(context.Screen, out int screenHeight, out int screenWidth);
        context.ScreenHeight = screenHeight;
        context.ScreenWidth = screenWidth;

        if (NCurses.HasColors())
        {
            CursesUI.InitializeColors();
        }
        else
        {
            // If terminal doesn't support colors we can't proceed with the intended UI
            NCurses.EndWin();
            Console.WriteLine("ERROR: Your terminal does not support colors");
            Environment.Exit(1);
        }

        string mode = "";
        if (!context.MainMenuShown)
        {
            mode = CursesUI.ShowMainMenu(context);
            SetSizeAndMineCount(mode);
            context.MainMenuShown = true;
        }

        // Top bar: single row for remaining mines and status
        context.TopScreen = NCurses.NewWindow(1, context.ScreenWidth, 0, 0);

        // Initialize minefield model
        context.Minefield = new(context.MinefieldScreenHeight, context.MinefieldScreenWidth, context.TotalMines);

        if (mode.Equals("Load"))
        {
            try
            {
                SaveManager.LoadGame(context);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        // Create the minefield window centered in the terminal. Add 2 for border.
        context.MinefieldScreen = NCurses.NewWindow(
            context.MinefieldScreenHeight + 2,
            context.MinefieldScreenWidth + 2,
            (context.ScreenHeight / 2) - (context.MinefieldScreenHeight / 2) - 2,
            (context.ScreenWidth / 2) - (context.MinefieldScreenWidth / 2) - 2
        );
        NCurses.Keypad(context.MinefieldScreen, true);

        return mode;
    }

    /// <summary>
    /// Map the chosen difficulty mode to minefield dimensions and mine count.
    /// Also enforce maximums based on terminal size to avoid creating oversized windows.
    /// </summary>
    /// <param name="mode">Mode string returned by the main menu.</param>
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
        else if (mode.Contains("Custom"))
        {
            CursesUI.ShowCustomMenu(context);
        }

        // Prevent minefield from exceeding terminal size (provide margins)
        if (context.MinefieldScreenHeight > context.ScreenHeight - 8)
        {
            context.MinefieldScreenHeight = context.ScreenHeight - 8;
        }
        if (context.MinefieldScreenWidth > context.ScreenWidth - 3)
        {
            context.MinefieldScreenWidth = context.ScreenWidth - 3;
        }

        // Ensure mines count is reasonable for the grid size
        if (context.MinefieldScreenHeight * context.MinefieldScreenWidth <= context.TotalMines)
        {
            context.TotalMines = context.MinefieldScreenHeight * context.MinefieldScreenWidth / 2;
        }
    }

    /// <summary>
    /// Main gameplay loop. Handles input, drawing, first-click mine generation, and termination conditions.
    /// Returns the last key that caused the loop to break (used by restart logic).
    /// </summary>
    /// <param name="mode">Selected mode (used to detect "Load").</param>
    /// <returns>Last key that caused game loop exit.</returns>
    private static int GameLoop(string mode)
    {
        int key = 0;
        Position exclude;
        bool gameStarted = false;

        while (!ShouldExit(key) && !context.WrongTileChosen && !(context.GameWon = context.Minefield.HasWon()))
        {
            if (key == 's') SaveManager.SaveGame(context);

            NCurses.ClearWindow(context.MinefieldScreen);
            CursesUI.DisplayMines(context, false);

            int colorPair = (int)ColorIds.None;
            Mines.Tile focusedMine = context.Minefield.Minefield[context.PlayerPositionYX.Row, context.PlayerPositionYX.Col];

            if (focusedMine.isOpened)
            {
                // Use number color when focused tile is opened
                colorPair = context.Minefield.Minefield[context.PlayerPositionYX.Row, context.PlayerPositionYX.Col].borderingMineCount;
            }
            else if (focusedMine.isFlagged)
            {
                // Highlight flagged tile
                colorPair = (int)ColorIds.Yellow;
            }

            CursesUI.DrawBox(context, colorPair); // Changes the color of the box based on focused tile's color

            NCurses.WindowRefresh(context.MinefieldScreen);

            CursesUI.DisplayRemainingMines(context);

            key = NCurses.WindowGetChar(context.MinefieldScreen);

            // On first move, generate mines excluding the first clicked tile to avoid instant loss
            if (!gameStarted && key == ' ' && !mode.Equals("Load"))
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

        return key;
    }

    /// <summary>
    /// Determines if the provided key should cause the game to exit the main play loop.
    /// </summary>
    private static bool ShouldExit(int key)
    {
        return key is 'q' or 'Q' or 'r' or 'R';
    }

    /// <summary>
    /// Update the player cursor position based on arrow key input while clamping to the minefield bounds.
    /// </summary>
    /// <param name="key">Input key code.</param>
    /// <returns>New player position.</returns>
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

    /// <summary>
    /// Wait for the user to press R or Q after a finished game. If R is pressed the application restarts.
    /// </summary>
    /// <param name="key">Initial key from game loop exit.</param>
    /// <returns>True if restart requested, false to quit.</returns>
    private static bool CheckForRestart(int key)
    {
        while (key is not 'r' and not 'R' and not 'q' and not 'Q')
        {
            key = NCurses.WindowGetChar(context.MinefieldScreen);
        }

        if (key is 'r' or 'R')
        {
            // Reset runtime flags and restart by reinitializing NCurses state
            context.WrongTileChosen = false;
            context.GameWon = false;

            NCurses.Clear();
            NCurses.Refresh();
            NCurses.EndWin();

            return true;
        }

        return false;
    }
}
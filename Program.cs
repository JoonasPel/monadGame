using System.Net.WebSockets;
using Newtonsoft.Json.Linq;

public class Program
{
  public static async Task Main()
  {
    string level = AskUserWhatLevelToPlay();
    (string playerToken, string levelIdToken) = LoadEnvVariables(level);
    if (playerToken == "" || levelIdToken == "")
    {
      await CloseProgram(websocket: null, msg: "Error loading env variables");
    }
    ClientUtils.SetTokens(playerToken, levelIdToken);
    string? gameData = await ClientUtils.CreateGame();
    if (gameData is null)
    {
      await CloseProgram(websocket: null, msg: "Could not start a new game");
    }
    ClientUtils.SetEntityId(gameData);
    ClientWebSocket websocket = await ClientUtils.ConnectWebsocket();
    bool subscribeSuccess = await ClientUtils.SubscribeToGame(websocket);
    if (!subscribeSuccess) await CloseProgram(websocket, msg: "game-sub fail");
    MovementLogic movement = new MovementLogic();
    Action printScore = ScorePrinter();
    while (true)
    {
      JObject? tickData = await ClientUtils.WaitForNextGameTick(websocket);
      if (tickData is null) continue;
      printScore();
      if (GameWon(tickData)) await CloseProgram(websocket, msg: "You Won :)");
      object? payload = movement.GenerateAction(tickData);
      if (payload is null) continue;
      Thread.Sleep(50);  // slow down to not get request limited
      string message = ClientUtils.CreateMessage(payload);
      await ClientUtils.SendMessage(websocket, message);
    }
  }

  private static async Task CloseProgram(ClientWebSocket? websocket, string msg)
  {
    if (websocket is not null)
    {
      try
      {
        await websocket.CloseAsync(
          WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
      }
      catch { }
    }
    Console.WriteLine($"\n{msg}\nGood Bye");
    Environment.Exit(0);
  }

  private static (string, string) LoadEnvVariables(string level)
  {
    DotNetEnv.Env.Load("./.env");
    string playerToken = Environment.GetEnvironmentVariable(
      "PLAYER_TOKEN") ?? "";
    string levelIdToken = Environment.GetEnvironmentVariable(
      $"LEVEL_ID_{level}") ?? "";
    return (playerToken, levelIdToken);
  }

  private static string AskUserWhatLevelToPlay()
  {
    Console.WriteLine($"What level do you want to play? Give Number only.");
    do
    {
      string? input = Console.ReadLine();
      if (int.TryParse(input, out _))
      {
        return input;
      }
      else
      {
        Console.WriteLine("Invalid Input! Try again.");
      }
    }
    while (true);
  }

  private static bool GameWon(JObject tickData)
  {
    try
    {
      return tickData["status"].ToString() == "FINISHED";
    }
    catch { }
    return false;
  }

  private static Action ScorePrinter()
  {
    int score = 0;
    return () =>
    {
      Console.Write($"\rScore: {score++}");
    };
  }
}





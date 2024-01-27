using System.Net.WebSockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class Program
{
  public static async Task Main()
  {
    (ClientWebSocket websocket, JObject gameData) = await Setup();
    MovementLogic movement = new MovementLogic(gameData);
    Action printScore = ScorePrinter();

    // Phase 1. Find target while updating graph, but dont go to the target yet
    while (true)
    {
      JObject? tickData = await ClientUtils.WaitForNextGameTick(websocket);
      if (tickData is null) continue;
      printScore();
      object? payload = movement.GenerateActionPhase1(tickData, out bool targetFound);
      if (targetFound) break;
      if (payload is null) continue;
      Thread.Sleep(50);  // slow down to not get request limited
      string message = ClientUtils.CreateMessage(payload);
      await ClientUtils.SendMessage(websocket, message);
    }

    // Phase 2. Reset, run BFS and using it, use shorter path to the target
    string resetMessage = ClientUtils.CreateMessage(movement.ResetAction());
    await ClientUtils.SendMessage(websocket, resetMessage);
    var enumerator = movement.GenerateActionPhase2().GetEnumerator();
    while (true)
    {
      Thread.Sleep(50);
      enumerator.MoveNext();
      Console.WriteLine(enumerator.Current);
      string message = ClientUtils.CreateMessage(enumerator.Current);
      await ClientUtils.SendMessage(websocket, message);
      continue;
      JObject? tickData = await ClientUtils.WaitForNextGameTick(websocket);
      if (GameWon(tickData))
      {
        movement.PrintGraph();
        await CloseProgram(websocket, msg: "You Won :)");
      }
    }
  }

  private static async Task<(ClientWebSocket, JObject)> Setup()
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
    JObject gameDataObj = Deserialize(gameData);
    ClientUtils.SetEntityId(gameDataObj);
    ClientWebSocket websocket = await ClientUtils.ConnectWebsocket();
    bool subscribeSuccess = await ClientUtils.SubscribeToGame(websocket);
    if (!subscribeSuccess) await CloseProgram(websocket, msg: "game-sub fail");
    return (websocket, gameDataObj);
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

  private static JObject Deserialize(string gameData)
  {
    JObject dataObject = JsonConvert.DeserializeObject<JObject>(gameData);
    return dataObject;
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





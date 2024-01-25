using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class Program
{
  public static async Task Main()
  {
    string level = AskUserWhatLevelToPlay();
    (string playerToken, string levelIdToken) = await LoadEnvVariables(level);
    ClientUtils.SetTokens(playerToken, levelIdToken);
    string? gameData = await ClientUtils.CreateGame();
    if (gameData is null) await CloseProgram(websocket: null);
    ClientUtils.SetEntityId(gameData);
    ClientWebSocket websocket = await ClientUtils.ConnectWebsocket();
    bool subscribeSuccess = await ClientUtils.SubscribeToGame(websocket);
    if (!subscribeSuccess) await CloseProgram(websocket);
    Action action = new Action();
    while (true)
    {
      JObject? gameState = await ClientUtils.WaitForNextGameTick(websocket);
      if (gameState is null) await CloseProgram(websocket);
      object? payload = action.GenerateAction(gameState);
      if (payload is null) continue;
      Thread.Sleep(50);  // slow down to not get request limited
      string message = ClientUtils.CreateMessage(payload);
      await ClientUtils.SendMessage(websocket, message);
    }
  }

  private static async Task CloseProgram(ClientWebSocket? websocket)
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
    Console.WriteLine("Good Bye");
    Environment.Exit(0);
  }

  private static async Task<(string, string)> LoadEnvVariables(string level)
  {
    DotNetEnv.Env.Load("./.env");
    string playerToken = Environment.GetEnvironmentVariable(
      "PLAYER_TOKEN") ?? "";
    string levelIdToken = Environment.GetEnvironmentVariable(
      $"LEVEL_ID_{level}") ?? "";
    if (playerToken == "")
    {
      Console.WriteLine("Can't find playerToken from .env");
      await CloseProgram(websocket: null);
    }
    if (levelIdToken == "")
    {
      Console.WriteLine("Can't find levelIdToken from .env");
      await CloseProgram(websocket: null);
    }
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
}





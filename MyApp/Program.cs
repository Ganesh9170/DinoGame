using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

class Program
{
    public static string LatestScore { get; set; }

    public static async Task Main()
    {
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false
        });

        var context = await browser.NewContextAsync(new()
        {
            
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync("https://elgoog.im/dinosaur-game/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Focus on the game
        await page.ClickAsync("body");
        await page.WaitForTimeoutAsync(1000);

        // Start the game

        // Log the score every second
        _ = Task.Run(async () =>
        {
            while (true)
            {
            try
            {
                var score = await page.EvaluateAsync<string>(@"
                () => {
                    return Runner.instance_.distanceMeter.digits.join('');
                }
                ");
                Console.WriteLine($"Score: {score}");
                // Store the latest score in a static variable for access outside this task
                Program.LatestScore = score;
            }
            catch { }
            await Task.Delay(1000);
            }
        });
        await page.Keyboard.PressAsync("Space");
        Console.WriteLine("Game started!");

        // Game loop
        while (true)
        {
            // Restart the game if crashed
            bool crashed = await page.EvaluateAsync<bool>("() => Runner.instance_.crashed");
            if (crashed)
            {
                Console.WriteLine("Game Over! Restarting...");
                await page.Keyboard.PressAsync("Space");
                await page.WaitForTimeoutAsync(2000);
                continue;
            }

            var obstacle = await page.EvaluateAsync<Obstacle>(@"
                () => {
                    const obs = Runner.instance_.horizon.obstacles[0];
                    if (!obs) return null;
                    return {
                        type: obs.typeConfig.type,
                        x: obs.xPos,
                        y: obs.yPos
                    };
                }
            ");

            if (obstacle != null && obstacle.x < 150)
            {
                if (obstacle.type == "PTERODACTYL")
                {
                    if (obstacle.y < 75)
                    {
                        Console.WriteLine("High bird! Ducking");
                        await page.Keyboard.DownAsync("ArrowDown");
                        await page.WaitForTimeoutAsync(500);
                        await page.Keyboard.UpAsync("ArrowDown");
                    }
                    else
                    {
                        Console.WriteLine("Low bird! Jumping");
                        await page.Keyboard.PressAsync("ArrowUp");
                    }
                }
                else
                {
                    Console.WriteLine("Cactus! Jumping");
                    await page.Keyboard.PressAsync("ArrowUp");
                }

                // Wait a bit to avoid double-triggering on the same obstacle
                await page.WaitForTimeoutAsync(400);
            }

            await page.WaitForTimeoutAsync(50); // frequent checks for responsiveness
        }
    }

    class Obstacle
    {
        public string type { get; set; }
        public float x { get; set; }
        public float y { get; set; }
    }
}
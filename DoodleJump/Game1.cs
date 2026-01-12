using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace DoodleJump
{
    // ================= GAME SETTINGS =================
    // Score must be stored/retrieved from here (requirement)
    public static class GameSettings
    {
        public static int CurrentScore { get; set; }
    }

    // ================= SCREEN BASE TYPES =================
    public abstract class Screen
    {
        public virtual void OnEnter() { }
        public abstract void Update(GameTime gt);
        public abstract void Draw(SpriteBatch sb);
    }

    /// <summary>
    /// BaseScreen:
    /// - Parent class for StartScreen, PlayScreen, EndScreen.
    /// - Stores current/previous keyboard states (protected) and updates them each frame.
    /// - Draws bottom info bar: left = current time (HH:mm:ss), middle = CenterInfoText,
    ///   right = score from GameSettings.
    /// </summary>
    public abstract class BaseScreen : Screen
    {
        protected KeyboardState currentKeyboardState;
        protected KeyboardState previousKeyboardState;

        protected string CenterInfoText = "";

        public override void OnEnter()
        {
            // sync to avoid "stuck Enter" when switching screens
            currentKeyboardState = previousKeyboardState = Keyboard.GetState();
        }

        protected void UpdateInputStates()
        {
            previousKeyboardState = currentKeyboardState;
            currentKeyboardState = Keyboard.GetState();
        }

        protected bool IsNewKeyPress(Keys key) =>
            currentKeyboardState.IsKeyDown(key) && previousKeyboardState.IsKeyUp(key);

        protected void DrawBaseInfo(SpriteBatch sb, int viewWidth, int viewHeight)
        {
            var pixel = Game1.Instance.pixel;
            var font = Game1.Instance.font;

            const int barHeight = 32;
            Rectangle barRect = new Rectangle(0, viewHeight - barHeight, viewWidth, barHeight);

            sb.Draw(pixel, barRect, new Color(0, 0, 0, 170));

            string timeText = DateTime.Now.ToString("HH:mm:ss");
            string scoreText = $"Score: {GameSettings.CurrentScore}";
            string centerText = CenterInfoText ?? "";

            Vector2 timeSize = font.MeasureString(timeText);
            Vector2 scoreSize = font.MeasureString(scoreText);
            Vector2 centerSize = font.MeasureString(centerText);

            float y = viewHeight - barHeight + (barHeight - timeSize.Y) / 2f;

            sb.DrawString(font, timeText, new Vector2(10, y), Color.White);
            sb.DrawString(font, scoreText, new Vector2(viewWidth - 10 - scoreSize.X, y), Color.White);

            if (!string.IsNullOrWhiteSpace(centerText))
                sb.DrawString(font, centerText, new Vector2((viewWidth - centerSize.X) / 2f, y), Color.White);
        }
    }

    // ================= GAME =================
    public class Game1 : Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        public Texture2D pixel;
        public SpriteFont font;

        public static Texture2D HeroTexture;
        public static Texture2D NormalPlatformTexture;
        public static Texture2D MovingPlatformTexture;
        public static Texture2D DisappearingPlatformTexture;
        public static Texture2D SpringPlatformTexture;
        public static Texture2D BombTexture;

        public static readonly Random Rng = new Random();

        ScreenManager screenManager;
        HighScoreManager highScoreManager;

        public static Game1 Instance;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Instance = this;
        }

        protected override void Initialize()
        {
            graphics.PreferredBackBufferWidth = 480;
            graphics.PreferredBackBufferHeight = 800;
            graphics.ApplyChanges();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            // LOAD FONT
            font = Content.Load<SpriteFont>("File");

            // LOAD HERO TEXTURE
            HeroTexture = Content.Load<Texture2D>("Hero/hero");

            // PLATFORM TEXTURES (procedural)
            NormalPlatformTexture = CreatePlatformTexture(GraphicsDevice, 80, 16, Color.Green, Color.DarkGreen);
            MovingPlatformTexture = CreatePlatformTexture(GraphicsDevice, 80, 16, Color.Green, Color.DarkGreen);
            DisappearingPlatformTexture = CreatePlatformTexture(GraphicsDevice, 80, 16, Color.White, Color.LightGray);
            SpringPlatformTexture = CreatePlatformTexture(GraphicsDevice, 80, 16, Color.Red, Color.DarkRed);

            // LOAD BOMB PNG (runtime file in Content/bomb.png)
            // This avoids MGCB changes: just copy the png to output.
            try
            {
                using var s = TitleContainer.OpenStream("Content/bomb.png");
                BombTexture = Texture2D.FromStream(GraphicsDevice, s);
            }
            catch
            {
                BombTexture = CreateFallbackBombTexture(GraphicsDevice);
            }

            highScoreManager = new HighScoreManager("highscores.txt");
            screenManager = new ScreenManager(highScoreManager);
            screenManager.Setup();

            GameSettings.CurrentScore = 0;
        }

        static Texture2D CreateFallbackBombTexture(GraphicsDevice device)
        {
            int size = 32;
            Texture2D tex = new Texture2D(device, size, size);
            Color[] data = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f));
                    if (dist < size / 2f - 2) data[y * size + x] = Color.DarkGray;
                    else if (dist < size / 2f) data[y * size + x] = Color.Black;
                    else data[y * size + x] = Color.Transparent;
                }
            }
            tex.SetData(data);
            return tex;
        }

        Texture2D CreatePlatformTexture(GraphicsDevice device, int width, int height, Color topColor, Color bottomColor)
        {
            Texture2D texture = new Texture2D(device, width, height);
            Color[] data = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / height;
                Color currentColor = Color.Lerp(topColor, bottomColor, t);

                for (int x = 0; x < width; x++)
                {
                    if (y < 2 || y >= height - 2)
                        data[y * width + x] = Color.Lerp(currentColor, Color.Black, 0.3f);
                    else if (x < 2 || x >= width - 2)
                        data[y * width + x] = Color.Lerp(currentColor, Color.Black, 0.2f);
                    else
                    {
                        bool isPattern = (x / 4 + y / 2) % 2 == 0;
                        data[y * width + x] = isPattern ? currentColor : Color.Lerp(currentColor, Color.White, 0.1f);
                    }
                }
            }

            texture.SetData(data);
            return texture;
        }

        protected override void Update(GameTime gameTime)
        {
            screenManager.Update(gameTime);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            screenManager.Draw(spriteBatch);
            base.Draw(gameTime);
        }
    }

    // ================= SCREEN MANAGER =================
    public class ScreenManager
    {
        readonly HighScoreManager highScore;

        StartScreen start;
        PlayScreen play;
        EndScreen end;

        GameState state = GameState.MainMenu;

        public ScreenManager(HighScoreManager hs)
        {
            highScore = hs;
            start = new StartScreen();
            play = new PlayScreen();
            end = new EndScreen();
        }

        public void Setup()
        {
            state = GameState.MainMenu;
            start.CurrentState = GameState.MainMenu;
            start.OnEnter();
            GameSettings.CurrentScore = 0;
        }

        public void Update(GameTime gt)
        {
            switch (state)
            {
                case GameState.MainMenu:
                case GameState.Paused:
                case GameState.Settings:
                    start.Update(gt);
                    if (state == GameState.Paused && start.CurrentState == GameState.Playing)
                    {
                        // resume
                        state = GameState.Playing;
                        play.OnEnter();
                    }
                    else if (state == GameState.MainMenu && start.CurrentState == GameState.Playing)
                    {
                        // new game
                        play = new PlayScreen();
                        play.OnEnter();
                        state = GameState.Playing;
                    }
                    else
                    {
                        state = start.CurrentState;
                    }
                    break;

                case GameState.Playing:
                    play.Update(gt);

                    if (play.PauseRequested)
                    {
                        play.PauseRequested = false;
                        state = GameState.Paused;
                        start.CurrentState = GameState.Paused;
                        start.OnEnter();
                        break;
                    }

                    if (play.IsGameOver)
                    {
                        play.IsGameOver = false;

                        int finalScore = GameSettings.CurrentScore;
                        highScore.AddScore("Player", finalScore);

                        end.SetFinalScore(finalScore);
                        end.OnEnter();
                        state = GameState.GameOver;
                    }
                    break;

                case GameState.GameOver:
                    end.Update(gt);
                    if (end.RestartRequested)
                    {
                        end.ResetRequests();
                        play = new PlayScreen();
                        play.OnEnter();
                        state = GameState.Playing;
                    }
                    else if (end.MenuRequested)
                    {
                        end.ResetRequests();
                        state = GameState.MainMenu;
                        start.CurrentState = GameState.MainMenu;
                        start.OnEnter();
                    }
                    break;
            }
        }

        public void Draw(SpriteBatch sb)
        {
            if (state == GameState.Playing)
                play.Draw(sb);
            else if (state == GameState.GameOver)
                end.Draw(sb);
            else
                start.Draw(sb);
        }
    }

    // ================= GAME STATE =================
    public enum GameState { MainMenu, Playing, Paused, GameOver, Settings }

    // ================= START SCREEN =================
    public class StartScreen : BaseScreen
    {
        GameState currentState = GameState.MainMenu;
        int selectedOption = 0;

        readonly List<string> mainMenuOptions = new List<string> { "Start Game", "Settings", "Exit" };
        readonly List<string> pauseMenuOptions = new List<string> { "Resume", "Main Menu", "Exit" };
        readonly List<string> settingsOptions = new List<string> { "Back" };

        float menuPulse = 0f;

        public GameState CurrentState
        {
            get => currentState;
            set
            {
                currentState = value;
                selectedOption = 0;
            }
        }

        public override void Update(GameTime gt)
        {
            UpdateInputStates();

            if (currentState == GameState.MainMenu)
            {
                CenterInfoText = "MAIN MENU";
                if (IsNewKeyPress(Keys.Up)) selectedOption = (selectedOption - 1 + mainMenuOptions.Count) % mainMenuOptions.Count;
                if (IsNewKeyPress(Keys.Down)) selectedOption = (selectedOption + 1) % mainMenuOptions.Count;
                if (IsNewKeyPress(Keys.Enter)) HandleMainMenuSelection();
            }
            else if (currentState == GameState.Paused)
            {
                CenterInfoText = "PAUSED";
                if (IsNewKeyPress(Keys.Up)) selectedOption = (selectedOption - 1 + pauseMenuOptions.Count) % pauseMenuOptions.Count;
                if (IsNewKeyPress(Keys.Down)) selectedOption = (selectedOption + 1) % pauseMenuOptions.Count;
                if (IsNewKeyPress(Keys.Enter)) HandlePauseMenuSelection();
            }
            else if (currentState == GameState.Settings)
            {
                CenterInfoText = "SETTINGS";
                // simple back with Enter/Esc
                if (IsNewKeyPress(Keys.Enter) || IsNewKeyPress(Keys.Escape))
                {
                    currentState = GameState.MainMenu;
                    selectedOption = 0;
                }
            }
        }

        void HandleMainMenuSelection()
        {
            switch (selectedOption)
            {
                case 0: currentState = GameState.Playing; break;
                case 1: currentState = GameState.Settings; break;
                case 2: Game1.Instance.Exit(); break;
            }
        }

        void HandlePauseMenuSelection()
        {
            switch (selectedOption)
            {
                case 0: currentState = GameState.Playing; break;
                case 1: currentState = GameState.MainMenu; break;
                case 2: Game1.Instance.Exit(); break;
            }
        }

        public override void Draw(SpriteBatch sb)
        {
            int viewWidth = 480;
            int viewHeight = 800;

            var font = Game1.Instance.font;
            var pixel = Game1.Instance.pixel;

            sb.Begin();

            // background gradient
            for (int y = 0; y < viewHeight; y++)
            {
                float t = (float)y / viewHeight;
                Color bgColor = Color.Lerp(new Color(15, 15, 35), new Color(25, 20, 40), t);
                sb.Draw(pixel, new Rectangle(0, y, viewWidth, 1), bgColor);
            }

            menuPulse += 0.03f;
            if (menuPulse > MathHelper.TwoPi) menuPulse -= MathHelper.TwoPi;

            string title = currentState == GameState.Paused ? "Paused"
                         : currentState == GameState.Settings ? "Settings"
                         : "Doodle Jump";

            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2((viewWidth - titleSize.X) / 2, 80);

            sb.DrawString(font, title, titlePos + new Vector2(3, 3), new Color(0, 0, 0, 150));
            float titleGlow = (float)(Math.Sin(menuPulse) * 0.3 + 0.7);
            Color titleColor = Color.Lerp(new Color(255, 200, 100), new Color(255, 255, 150), titleGlow);
            sb.DrawString(font, title, titlePos, titleColor);

            List<string> options = currentState == GameState.MainMenu ? mainMenuOptions
                                 : currentState == GameState.Paused ? pauseMenuOptions
                                 : settingsOptions;

            for (int i = 0; i < options.Count; i++)
            {
                Vector2 optionSize = font.MeasureString(options[i]);
                Vector2 optionPos = new Vector2((viewWidth - optionSize.X) / 2, 250 + i * 70);

                bool isSelected = i == selectedOption && currentState != GameState.Settings;

                if (isSelected)
                {
                    int padding = 15;
                    Rectangle optionBg = new Rectangle((int)optionPos.X - padding, (int)optionPos.Y - 5,
                        (int)optionSize.X + padding * 2, (int)optionSize.Y + 10);

                    Color bg1 = new Color(100, 150, 255, 150);
                    Color bg2 = new Color(150, 100, 255, 150);
                    for (int j = 0; j < optionBg.Height; j++)
                    {
                        float t = (float)j / optionBg.Height;
                        Color gradColor = Color.Lerp(bg1, bg2, t);
                        sb.Draw(pixel, new Rectangle(optionBg.X, optionBg.Y + j, optionBg.Width, 1), gradColor);
                    }

                    float borderGlow = (float)(Math.Sin(menuPulse * 2) * 0.5 + 0.5);
                    Color borderColor = Color.Lerp(new Color(150, 200, 255), new Color(255, 200, 255), borderGlow);
                    sb.Draw(pixel, new Rectangle(optionBg.X, optionBg.Y, optionBg.Width, 2), borderColor);
                    sb.Draw(pixel, new Rectangle(optionBg.X, optionBg.Y + optionBg.Height - 2, optionBg.Width, 2), borderColor);
                    sb.Draw(pixel, new Rectangle(optionBg.X, optionBg.Y, 2, optionBg.Height), borderColor);
                    sb.Draw(pixel, new Rectangle(optionBg.X + optionBg.Width - 2, optionBg.Y, 2, optionBg.Height), borderColor);

                    float arrowOffset = (float)(Math.Sin(menuPulse * 3) * 5);
                    sb.DrawString(font, ">", new Vector2(optionPos.X - 30 + arrowOffset, optionPos.Y), Color.Yellow);
                    sb.DrawString(font, "<", new Vector2(optionPos.X + optionSize.X + 10 - arrowOffset, optionPos.Y), Color.Yellow);
                }

                Color textColor = isSelected ? Color.White : new Color(200, 200, 200);
                sb.DrawString(font, options[i], optionPos, textColor);
            }

            DrawBaseInfo(sb, viewWidth, viewHeight);
            sb.End();
        }
    }

    // ================= END SCREEN =================
    public class EndScreen : BaseScreen
    {
        public bool RestartRequested { get; private set; }
        public bool MenuRequested { get; private set; }

        int finalScore;

        public void SetFinalScore(int score)
        {
            finalScore = score;
            GameSettings.CurrentScore = score;
            RestartRequested = false;
            MenuRequested = false;
            CenterInfoText = "GAME OVER";
        }

        public void ResetRequests()
        {
            RestartRequested = false;
            MenuRequested = false;
        }

        public override void Update(GameTime gt)
        {
            UpdateInputStates();
            CenterInfoText = "GAME OVER";

            if (IsNewKeyPress(Keys.Enter)) RestartRequested = true;
            if (IsNewKeyPress(Keys.Escape)) MenuRequested = true;
        }

        public override void Draw(SpriteBatch sb)
        {
            int viewWidth = 480;
            int viewHeight = 800;

            var pixel = Game1.Instance.pixel;
            var font = Game1.Instance.font;

            sb.Begin();

            sb.Draw(pixel, new Rectangle(0, 0, viewWidth, viewHeight), new Color(0, 0, 0, 200));

            int modalWidth = 320;
            int modalHeight = 220;
            int modalX = (viewWidth - modalWidth) / 2;
            int modalY = (viewHeight - modalHeight) / 2;

            sb.Draw(pixel, new Rectangle(modalX, modalY, modalWidth, modalHeight), new Color(40, 40, 40));
            sb.Draw(pixel, new Rectangle(modalX + 3, modalY + 3, modalWidth - 6, modalHeight - 6), new Color(70, 70, 70));

            string title = "Game Over";
            string score = $"Score: {finalScore}";
            string hint1 = "Enter - Restart";
            string hint2 = "Esc - Main Menu";

            Vector2 tSize = font.MeasureString(title);
            Vector2 sSize = font.MeasureString(score);

            sb.DrawString(font, title, new Vector2(modalX + (modalWidth - tSize.X) / 2, modalY + 30), Color.Red);
            sb.DrawString(font, score, new Vector2(modalX + (modalWidth - sSize.X) / 2, modalY + 85), Color.White);
            sb.DrawString(font, hint1, new Vector2(modalX + 30, modalY + 140), Color.Yellow);
            sb.DrawString(font, hint2, new Vector2(modalX + 30, modalY + 170), Color.Yellow);

            DrawBaseInfo(sb, viewWidth, viewHeight);
            sb.End();
        }
    }

    // ================= GAME OBJECT =================
    public abstract class GameObject
    {
        public Vector2 Position;
        public Vector2 Size;

        public Rectangle Bounds =>
            new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y);

        public virtual void Update(GameTime gt, PlayScreen screen) { }
        public abstract void Draw(SpriteBatch sb, Texture2D pixel);
    }

    // ================= PLAYER =================
    public class Player : GameObject
    {
        Texture2D texture;
        public Vector2 Velocity;
        public float StartY;
        public float HighestY;
        public int Score => (int)Math.Max(0, (StartY - HighestY) / 10f) + Combo.GetBonusScore();

        // Power-ups
        public bool HasJetpack = false;
        public bool HasShield = false;
        public bool HasSlowMotion = false;
        public bool HasInvincibility = false;
        public bool HasSpeedBoost = false;
        public bool HasDoubleJump = false;
        int jumpCount = 0;
        bool canDoubleJump = false;
        public float powerUpTimer = 0f;
        public float powerUpDuration = 5f;

        public int CoinScore = 0;

        PlayerAnimation animation = new PlayerAnimation();
        bool wasJumpPressed = false;

        public ComboSystem Combo = new ComboSystem();
        public float gameTime = 0f;

        public Player()
        {
            texture = Game1.HeroTexture;
            Size = new Vector2(48, 48);
            Position = new Vector2(220, 700);
            Velocity = Vector2.Zero;
            StartY = Position.Y;
            HighestY = Position.Y;
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;
            gameTime += dt;
            Combo.Update(gt);

            var ks = Keyboard.GetState();
            float speed = HasSpeedBoost ? 400f : 250f;

            // wind event
            if (screen.specialEvent.Type == SpecialEventType.Wind && screen.specialEvent.IsActive)
            {
                float windForce = 150f;
                Position.X += windForce * dt * (float)(Math.Sin(gameTime * 2) * 0.5 + 0.5);
            }

            if (ks.IsKeyDown(Keys.Left) || ks.IsKeyDown(Keys.A))
                Position.X -= speed * dt;
            if (ks.IsKeyDown(Keys.Right) || ks.IsKeyDown(Keys.D))
                Position.X += speed * dt;

            if (Position.X < -Size.X) Position.X = screen.ViewWidth;
            if (Position.X > screen.ViewWidth) Position.X = -Size.X;

            animation.Update(gt, Velocity);

            // power-up timer
            if (powerUpTimer > 0)
            {
                powerUpTimer -= dt;
                if (powerUpTimer <= 0)
                {
                    HasJetpack = false;
                    HasShield = false;
                    HasSlowMotion = false;
                    HasInvincibility = false;
                    HasSpeedBoost = false;
                    HasDoubleJump = false;
                }
            }

            if (HasJetpack && Velocity.Y > 0)
                Velocity.Y -= 300f * dt;

            float gravity = HasSlowMotion ? 450f : 900f;
            Velocity.Y += gravity * dt;
            Position += Velocity * dt;

            if (Position.Y < HighestY)
                HighestY = Position.Y;

            bool jumpPressed = ks.IsKeyDown(Keys.Space) || ks.IsKeyDown(Keys.Up) || ks.IsKeyDown(Keys.W);
            if (HasDoubleJump && canDoubleJump && jumpPressed && !wasJumpPressed)
            {
                if (jumpCount < 1 && Velocity.Y > 0)
                {
                    Velocity.Y = -450f;
                    jumpCount = 1;
                    canDoubleJump = false;
                }
            }
            wasJumpPressed = jumpPressed;

            if (Velocity.Y > 0)
            {
                bool landed = false;
                foreach (var p in screen.Platforms)
                {
                    if (p.IsActive &&
                        Bounds.Bottom >= p.Bounds.Top &&
                        Bounds.Bottom <= p.Bounds.Top + 10 &&
                        Bounds.Right > p.Bounds.Left &&
                        Bounds.Left < p.Bounds.Right)
                    {
                        Position.Y = p.Bounds.Top - Size.Y;
                        p.OnPlayerJump(this);

                        // Bombs spawn on the platform you just landed on
                        screen.SpawnBombOnPlatform(p);

                        Combo.OnJump(gameTime);

                        Color jumpColor = p is SpringPlatform ? Color.Orange : Color.LightBlue;
                        screen.particleSystem.CreateJumpEffect(
                            new Vector2(Position.X + Size.X / 2, p.Bounds.Top),
                            jumpColor
                        );

                        landed = true;
                        jumpCount = 0;
                        canDoubleJump = true;
                    }
                }

                if (!landed && Velocity.Y > 50)
                    canDoubleJump = true;
            }

            if (Position.Y > screen.CameraY + screen.ViewHeight + 200)
                screen.GameOver();
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            float rotation = animation.GetRotation(Velocity);
            Color tint = animation.GetTint();

            if (HasShield)
            {
                Rectangle shieldRect = new Rectangle((int)Position.X - 5, (int)Position.Y - 5, (int)Size.X + 10, (int)Size.Y + 10);
                sb.Draw(pixel, shieldRect, new Color(0, 255, 255, 100));
            }

            if (HasInvincibility)
            {
                Rectangle invincRect = new Rectangle((int)Position.X - 8, (int)Position.Y - 8, (int)Size.X + 16, (int)Size.Y + 16);
                float pulse = (float)(Math.Sin(powerUpTimer * 10) * 0.3 + 0.7);
                sb.Draw(pixel, invincRect, new Color(255, 0, 255, (int)(100 * pulse)));
            }

            Color drawColor = HasInvincibility ? Color.Lerp(tint, Color.Purple, 0.5f) : tint;

            Vector2 origin = new Vector2(texture.Width / 2f, texture.Height / 2f);
            Vector2 pos = Position + Size / 2f;
            Vector2 scale = new Vector2(Size.X / texture.Width, Size.Y / texture.Height);

            sb.Draw(texture, pos, null, drawColor, rotation, origin, scale, SpriteEffects.None, 0f);
        }

        public void ApplyPowerUp(PowerUp.PowerUpType type)
        {
            switch (type)
            {
                case PowerUp.PowerUpType.Jetpack: HasJetpack = true; break;
                case PowerUp.PowerUpType.Shield: HasShield = true; break;
                case PowerUp.PowerUpType.SlowMotion: HasSlowMotion = true; break;
                case PowerUp.PowerUpType.Invincibility: HasInvincibility = true; break;
                case PowerUp.PowerUpType.SpeedBoost: HasSpeedBoost = true; break;
                case PowerUp.PowerUpType.DoubleJump: HasDoubleJump = true; break;
            }
            powerUpTimer = powerUpDuration;
        }

        public void CollectCoin()
        {
            CoinScore += 10;
        }

        public int TotalScore => Score + CoinScore;
    }

    // ================= PLATFORM (BASE) =================
    public abstract class Platform : GameObject
    {
        public bool IsActive = true;
        public bool HasBomb = false; // prevents multiple bombs on same platform
        protected Texture2D texture;

        public Platform(float x, float y)
        {
            Position = new Vector2(x, y);
            Size = new Vector2(80, 16);
        }

        public virtual void OnPlayerJump(Player player)
        {
            player.Velocity.Y = -450f;
        }

        public override void Update(GameTime gt, PlayScreen screen) { }
    }

    public class NormalPlatform : Platform
    {
        public NormalPlatform(float x, float y) : base(x, y) => texture = Game1.NormalPlatformTexture;
        public override void Draw(SpriteBatch sb, Texture2D pixel) { if (IsActive) sb.Draw(texture, Bounds, Color.White); }
    }

    public class MovingPlatform : Platform
    {
        float startX;
        float endX;
        float speed;
        bool movingRight = true;

        public MovingPlatform(float x, float y, float range, float moveSpeed) : base(x, y)
        {
            startX = x - range / 2;
            endX = x + range / 2;
            speed = moveSpeed;
            texture = Game1.MovingPlatformTexture;
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            if (!IsActive) return;
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;

            if (movingRight)
            {
                Position.X += speed * dt;
                if (Position.X >= endX) { Position.X = endX; movingRight = false; }
            }
            else
            {
                Position.X -= speed * dt;
                if (Position.X <= startX) { Position.X = startX; movingRight = true; }
            }
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel) { if (IsActive) sb.Draw(texture, Bounds, Color.White); }
    }

    public class DisappearingPlatform : Platform
    {
        float disappearTimer = 0f;
        float disappearDelay = 0.5f;
        bool playerJumped = false;

        public DisappearingPlatform(float x, float y, float delay = 0.5f) : base(x, y)
        {
            texture = Game1.DisappearingPlatformTexture;
            disappearDelay = delay;
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            if (!IsActive) return;
            if (playerJumped)
            {
                disappearTimer += (float)gt.ElapsedGameTime.TotalSeconds;
                if (disappearTimer >= disappearDelay)
                    IsActive = false;
            }
        }

        public override void OnPlayerJump(Player player)
        {
            if (!playerJumped)
            {
                playerJumped = true;
                base.OnPlayerJump(player);
            }
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            if (!IsActive) return;

            Color color = playerJumped && disappearTimer > disappearDelay * 0.5f
                ? Color.Lerp(Color.White, Color.Transparent, (disappearTimer - disappearDelay * 0.5f) / (disappearDelay * 0.5f))
                : Color.White;

            sb.Draw(texture, Bounds, color);
        }
    }

    public class SpringPlatform : Platform
    {
        float springAnimation = 0f;

        public SpringPlatform(float x, float y) : base(x, y) => texture = Game1.SpringPlatformTexture;

        public override void OnPlayerJump(Player player) => player.Velocity.Y = -650f;

        public override void Update(GameTime gt, PlayScreen screen)
        {
            springAnimation += (float)gt.ElapsedGameTime.TotalSeconds * 5f;
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            if (!IsActive) return;

            sb.Draw(texture, Bounds, Color.White);

            float springOffset = (float)(Math.Sin(springAnimation) * 2);
            Rectangle springRect = new Rectangle((int)Position.X + 15, (int)Position.Y + 2 + (int)springOffset, (int)Size.X - 30, 12);

            Color springColor1 = Color.Yellow;
            Color springColor2 = Color.Orange;
            for (int i = 0; i < springRect.Height; i++)
            {
                float t = (float)i / springRect.Height;
                Color springColor = Color.Lerp(springColor1, springColor2, t);
                sb.Draw(pixel, new Rectangle(springRect.X, springRect.Y + i, springRect.Width, 1), springColor);
            }

            for (int x = springRect.X; x < springRect.X + springRect.Width; x += 8)
                sb.Draw(pixel, new Rectangle(x, springRect.Y, 2, springRect.Height), Color.Orange);
        }
    }

    // ================= PLAY SCREEN =================
    public class PlayScreen : BaseScreen
    {
        public Player Player;
        public List<Platform> Platforms = new List<Platform>();
        public List<Enemy> Enemies = new List<Enemy>();
        public List<Obstacle> Obstacles = new List<Obstacle>();
        public List<PowerUp> PowerUps = new List<PowerUp>();
        public List<Coin> Coins = new List<Coin>();
        public List<Boss> Bosses = new List<Boss>();
        public List<Bomb> Bombs = new List<Bomb>();

        ParallaxBackground background;
        public ParticleSystem particleSystem;
        public SpecialEvent specialEvent = new SpecialEvent();

        Random rnd = Game1.Rng;

        float lastBossSpawnHeight = 0f;
        float bossSpawnInterval = 2000f;

        public int ViewWidth => 480;
        public int ViewHeight => 800;

        public float CameraY = 0f;
        float highestPlatformY = 0f;

        public bool IsGameOver = false;
        public bool PauseRequested = false;

        float scorePulse = 0f;

        public PlayScreen()
        {
            background = new ParallaxBackground(Game1.Instance.GraphicsDevice);
            particleSystem = new ParticleSystem(Game1.Instance.pixel);
            Start();
        }

        public void Start()
        {
            Player = new Player();

            Platforms.Clear();
            Enemies.Clear();
            Obstacles.Clear();
            PowerUps.Clear();
            Coins.Clear();
            Bosses.Clear();
            Bombs.Clear();

            CameraY = 0f;
            IsGameOver = false;
            PauseRequested = false;

            lastBossSpawnHeight = 0f;

            float playerStartY = Player.Position.Y;
            float platformY = playerStartY + Player.Size.Y;
            float platformX = Player.Position.X + (Player.Size.X / 2) - 40;

            Platforms.Add(new NormalPlatform(platformX, platformY));

            highestPlatformY = 0f;
            GeneratePlatforms(760, 0, 20);
        }

        void GeneratePlatforms(float startY, float endY, int count)
        {
            if (count <= 0) return;

            float y = startY;
            float totalDistance = Math.Abs(startY - endY);
            float step = totalDistance / count;

            for (int i = 0; i < count; i++)
            {
                Platform platform = CreateRandomPlatform(rnd.Next(40, 360), y);
                Platforms.Add(platform);

                if (y < highestPlatformY)
                    highestPlatformY = y;

                y -= step;
            }
        }

        // Difficulty via platform types (not impossible gaps)
        Platform CreateRandomPlatform(float x, float y)
        {
            float height = Math.Max(0f, -y);
            float d = MathHelper.Clamp(height / 6000f, 0f, 1f);

            int normalChance = (int)Math.Round(MathHelper.Lerp(65f, 25f, d));
            int movingChance = (int)Math.Round(MathHelper.Lerp(20f, 35f, d));
            int disappearingChance = (int)Math.Round(MathHelper.Lerp(10f, 30f, d));
            int springChance = 100 - normalChance - movingChance - disappearingChance;

            if (springChance < 5)
            {
                int deficit = 5 - springChance;
                springChance = 5;
                normalChance = Math.Max(10, normalChance - deficit);
            }

            int roll = rnd.Next(0, 100);

            if (roll < normalChance)
                return new NormalPlatform(x, y);

            roll -= normalChance;

            if (roll < movingChance)
            {
                float range = MathHelper.Lerp(70f, 170f, d) + rnd.Next(-15, 16);
                float speed = MathHelper.Lerp(70f, 200f, d) + rnd.Next(-20, 21);

                range = MathHelper.Clamp(range, 60f, 200f);
                speed = MathHelper.Clamp(speed, 60f, 240f);

                return new MovingPlatform(x, y, range, speed);
            }

            roll -= movingChance;

            if (roll < disappearingChance)
            {
                float delay = MathHelper.Lerp(0.55f, 0.18f, d) + (float)(rnd.NextDouble() * 0.06 - 0.03f);
                delay = MathHelper.Clamp(delay, 0.15f, 0.60f);
                return new DisappearingPlatform(x, y, delay);
            }

            return new SpringPlatform(x, y);
        }

        public void SpawnBombOnPlatform(Platform platform)
        {
            if (platform == null || !platform.IsActive || platform.HasBomb)
                return;

            // Difficulty increases with height: more bombs the higher you go.
            float height = Math.Max(0f, -platform.Position.Y);
            float d = MathHelper.Clamp(height / 6000f, 0f, 1f);
            float chance = MathHelper.Lerp(0.05f, 0.35f, d); // 5% -> 35%

            if (rnd.NextDouble() > chance)
                return;

            // Prevent overload
            const int maxBombs = 12;
            if (Bombs.Count >= maxBombs)
            {
                var oldest = Bombs[0];
                if (oldest.Parent != null) oldest.Parent.HasBomb = false;
                Bombs.RemoveAt(0);
            }

            platform.HasBomb = true;
            Bombs.Add(new Bomb(platform));
        }

        public override void Update(GameTime gt)
        {
            UpdateInputStates();

            if (IsNewKeyPress(Keys.Escape))
            {
                PauseRequested = true;
                CenterInfoText = "PAUSED";
                return;
            }

            float playerY = Player.Position.Y;

            float targetCameraY = Player.Position.Y - ViewHeight * 0.3f;
            if (targetCameraY < CameraY)
                CameraY = targetCameraY;

            Platforms.RemoveAll(p => p.Position.Y > CameraY + ViewHeight + 200);

            float currentTop = highestPlatformY;
            float targetTop = playerY - ViewHeight * 1.5f;

            // Keep gaps always reachable
            float height = Math.Max(0f, -playerY);
            float difficulty = MathHelper.Clamp(height / 6000f, 0f, 1f);

            const float jumpV = 450f;
            const float g = 900f;
            float maxReach = (jumpV * jumpV) / (2f * g);    // 112.5
            float safeMax = maxReach * 0.90f;               // ~101
            float safeMin = maxReach * 0.60f;               // ~67

            float minDistance = MathHelper.Lerp(45f, safeMin, difficulty);
            float maxDistance = MathHelper.Lerp(70f, safeMax, difficulty);

            if (currentTop > targetTop)
            {
                while (currentTop > targetTop)
                {
                    int minD = (int)Math.Round(minDistance);
                    int maxD = (int)Math.Round(maxDistance);
                    if (maxD <= minD) maxD = minD + 1;

                    float distance = rnd.Next(minD, maxD + 1);
                    currentTop -= distance;

                    Platform platform = CreateRandomPlatform(rnd.Next(40, 360), currentTop);
                    Platforms.Add(platform);

                    if (currentTop < highestPlatformY)
                        highestPlatformY = currentTop;
                }
            }

            background.Update(CameraY);

            Enemies.RemoveAll(e => e.Position.Y > CameraY + ViewHeight + 200);
            Obstacles.RemoveAll(o => o.Position.Y > CameraY + ViewHeight + 200);
            PowerUps.RemoveAll(p => p.Position.Y > CameraY + ViewHeight + 200);
            Coins.RemoveAll(c => c.Position.Y > CameraY + ViewHeight + 200);
            Bosses.RemoveAll(b => b.Health <= 0 || b.Position.Y > CameraY + ViewHeight + 200);

            // Bomb cleanup (keep platform flag consistent)
            for (int i = Bombs.Count - 1; i >= 0; i--)
            {
                var bomb = Bombs[i];
                bool parentGone = bomb.Parent == null || !bomb.Parent.IsActive;
                bool farBelow = (bomb.Parent != null && bomb.Parent.Position.Y > CameraY + ViewHeight + 240);
                if (parentGone || farBelow)
                {
                    if (bomb.Parent != null) bomb.Parent.HasBomb = false;
                    Bombs.RemoveAt(i);
                }
            }

            float currentHeight = -Player.Position.Y;
            if (currentHeight - lastBossSpawnHeight >= bossSpawnInterval)
            {
                lastBossSpawnHeight = currentHeight;
                float spawnY = Player.Position.Y - ViewHeight * 0.5f;
                Bosses.Add(new Boss(rnd.Next(60, 360), spawnY, rnd.Next(40, 70), rnd.Next(100, 200)));
            }

            // Spawn extra objects
            if (rnd.Next(0, 100) < 4)
            {
                float spawnY = playerY - ViewHeight * 0.8f;

                int type = rnd.Next(0, 100);
                if (type < 15)
                    Enemies.Add(new Enemy(rnd.Next(40, 360), spawnY, rnd.Next(30, 60), rnd.Next(50, 100)));
                else if (type < 25)
                    Enemies.Add(new FlyingEnemy(rnd.Next(40, 360), spawnY, rnd.Next(30, 60), rnd.Next(50, 100), rnd.Next(20, 40), rnd.Next(30, 60)));
                else if (type < 35)
                    Enemies.Add(new ShootingEnemy(rnd.Next(40, 360), spawnY, rnd.Next(30, 60), rnd.Next(50, 100)));
                else if (type < 40)
                {
                    var chasingEnemy = new ChasingEnemy(rnd.Next(40, 360), spawnY, rnd.Next(30, 60), rnd.Next(50, 100));
                    chasingEnemy.SetTarget(Player);
                    Enemies.Add(chasingEnemy);
                }
                else if (type < 45) // reduced black hole chance (was too high)
                    Enemies.Add(new BlackHole(rnd.Next(40, 360), spawnY));
                else if (type < 55)
                    Obstacles.Add(new Obstacle(rnd.Next(40, 360), spawnY));
                else if (type < 70)
                {
                    PowerUp.PowerUpType powerType = (PowerUp.PowerUpType)rnd.Next(0, 6);
                    PowerUps.Add(new PowerUp(rnd.Next(40, 360), spawnY, powerType));
                }
                else if (type < 85)
                    Coins.Add(new Coin(rnd.Next(40, 360), spawnY));
            }

            foreach (var platform in Platforms)
                platform.Update(gt, this);

            foreach (var bomb in Bombs)
                bomb.Update(gt, this);

            foreach (var enemy in Enemies)
            {
                enemy.Update(gt, this);

                if (enemy is ShootingEnemy shootingEnemy)
                {
                    foreach (var bullet in shootingEnemy.Bullets.ToList())
                    {
                        if (bullet.CheckCollision(Player))
                        {
                            if (!Player.HasInvincibility && !Player.HasShield)
                                GameOver();
                            shootingEnemy.Bullets.Remove(bullet);
                        }
                    }
                }
            }

            foreach (var powerUp in PowerUps)
                powerUp.Update(gt, this);
            foreach (var coin in Coins)
                coin.Update(gt, this);
            foreach (var boss in Bosses)
                boss.Update(gt, this);

            Player.Update(gt, this);

            foreach (var enemy in Enemies.ToList())
            {
                if (enemy.CheckCollision(Player))
                {
                    if (!Player.HasInvincibility && !Player.HasShield)
                        GameOver();
                    else
                        Enemies.Remove(enemy);
                    break;
                }
            }

            foreach (var obstacle in Obstacles.ToList())
            {
                if (obstacle.CheckCollision(Player))
                {
                    if (!Player.HasInvincibility && !Player.HasShield)
                        GameOver();
                    else
                        Obstacles.Remove(obstacle);
                    break;
                }
            }

            // Bombs
            foreach (var bomb in Bombs.ToList())
            {
                if (!bomb.IsArmed)
                    continue;

                if (bomb.CheckCollision(Player))
                {
                    if (!Player.HasInvincibility && !Player.HasShield)
                    {
                        GameOver();
                    }
                    else
                    {
                        // Shield / invincibility destroys the bomb
                        if (bomb.Parent != null) bomb.Parent.HasBomb = false;
                        Bombs.Remove(bomb);
                    }
                    break;
                }
            }

            foreach (var powerUp in PowerUps.ToList())
            {
                if (powerUp.CheckCollision(Player))
                {
                    Player.ApplyPowerUp(powerUp.Type);

                    Color powerUpColor = powerUp.Type == PowerUp.PowerUpType.Jetpack ? Color.Blue :
                                        powerUp.Type == PowerUp.PowerUpType.Shield ? Color.Cyan :
                                        powerUp.Type == PowerUp.PowerUpType.SlowMotion ? Color.Yellow :
                                        powerUp.Type == PowerUp.PowerUpType.Invincibility ? Color.Purple :
                                        powerUp.Type == PowerUp.PowerUpType.SpeedBoost ? Color.Orange :
                                        Color.Green;

                    particleSystem.CreateCollectEffect(
                        new Vector2(powerUp.Position.X + powerUp.Size.X / 2, powerUp.Position.Y + powerUp.Size.Y / 2),
                        powerUpColor
                    );

                    PowerUps.Remove(powerUp);
                }
            }

            foreach (var coin in Coins.ToList())
            {
                if (coin.CheckCollision(Player))
                {
                    Player.CollectCoin();
                    particleSystem.CreateCollectEffect(
                        new Vector2(coin.Position.X + coin.Size.X / 2, coin.Position.Y + coin.Size.Y / 2),
                        Color.Gold
                    );
                    Coins.Remove(coin);
                }
            }

            foreach (var boss in Bosses.ToList())
            {
                if (boss.CheckCollision(Player))
                {
                    if (!Player.HasInvincibility && !Player.HasShield)
                        GameOver();
                }

                foreach (var bullet in boss.Bullets.ToList())
                {
                    if (bullet.CheckCollision(Player))
                    {
                        if (!Player.HasInvincibility && !Player.HasShield)
                            GameOver();
                        boss.Bullets.Remove(bullet);
                    }
                }

                if (Player.Velocity.Y > 0 &&
                    Player.Bounds.Bottom >= boss.Bounds.Top &&
                    Player.Bounds.Bottom <= boss.Bounds.Top + 20 &&
                    Player.Bounds.Right > boss.Bounds.Left &&
                    Player.Bounds.Left < boss.Bounds.Right)
                {
                    if (boss.TakeDamage())
                    {
                        particleSystem.CreateComboEffect(
                            new Vector2(boss.Position.X + boss.Size.X / 2, boss.Position.Y + boss.Size.Y / 2),
                            20
                        );
                        Player.Combo.OnJump(Player.gameTime);
                        Bosses.Remove(boss);
                    }
                    else
                    {
                        Player.Velocity.Y = -400f;
                    }
                }
            }

            particleSystem.Update(gt);
            specialEvent.Update(gt, this);

            if (rnd.Next(0, 10000) < 2)
            {
                if (rnd.Next(0, 2) == 0)
                    specialEvent.Start(SpecialEventType.MeteorShower, 10f);
                else
                    specialEvent.Start(SpecialEventType.Wind, 8f);
            }

            // Keep GameSettings score updated (requirement)
            GameSettings.CurrentScore = Player.TotalScore;

            // Center info for BaseScreen bar
            if (Player.Combo.CurrentCombo > 0)
                CenterInfoText = $"COMBO x{Player.Combo.CurrentCombo}";
            else if (specialEvent.IsActive)
                CenterInfoText = specialEvent.Type == SpecialEventType.MeteorShower ? "METEOR SHOWER"
                               : specialEvent.Type == SpecialEventType.Wind ? "WIND"
                               : "";
            else
                CenterInfoText = "";
        }

        public override void Draw(SpriteBatch sb)
        {
            var pixel = Game1.Instance.pixel;
            var font = Game1.Instance.font;

            // background without camera
            sb.Begin();
            background.Draw(sb);
            sb.End();

            Matrix transform = Matrix.CreateTranslation(0, -CameraY, 0);
            sb.Begin(transformMatrix: transform);

            foreach (var p in Platforms)
                if (p.Position.Y >= CameraY - 100 && p.Position.Y <= CameraY + ViewHeight + 100)
                    p.Draw(sb, pixel);

            foreach (var bomb in Bombs)
                if (bomb.Position.Y >= CameraY - 150 && bomb.Position.Y <= CameraY + ViewHeight + 150)
                    bomb.Draw(sb, pixel);

            foreach (var enemy in Enemies)
                if (enemy.Position.Y >= CameraY - 100 && enemy.Position.Y <= CameraY + ViewHeight + 100)
                    enemy.Draw(sb, pixel);

            foreach (var obstacle in Obstacles)
                if (obstacle.Position.Y >= CameraY - 100 && obstacle.Position.Y <= CameraY + ViewHeight + 100)
                    obstacle.Draw(sb, pixel);

            foreach (var powerUp in PowerUps)
                if (powerUp.Position.Y >= CameraY - 100 && powerUp.Position.Y <= CameraY + ViewHeight + 100)
                    powerUp.Draw(sb, pixel);

            foreach (var coin in Coins)
                if (coin.Position.Y >= CameraY - 100 && coin.Position.Y <= CameraY + ViewHeight + 100)
                    coin.Draw(sb, pixel);

            foreach (var enemy in Enemies)
                if (enemy is ShootingEnemy shootingEnemy)
                    foreach (var bullet in shootingEnemy.Bullets)
                        if (bullet.Position.Y >= CameraY - 100 && bullet.Position.Y <= CameraY + ViewHeight + 100)
                            bullet.Draw(sb, pixel);

            foreach (var boss in Bosses)
                if (boss.Position.Y >= CameraY - 200 && boss.Position.Y <= CameraY + ViewHeight + 200)
                    boss.Draw(sb, pixel);

            Player.Draw(sb, pixel);
            particleSystem.Draw(sb, CameraY);

            sb.End();

            sb.Begin();
            DrawScoreUI(sb, pixel, font);
            DrawPowerUpIndicators(sb, pixel, font);
            DrawComboUI(sb, pixel, font);
            DrawSpecialEventUI(sb, pixel, font);

            // BaseScreen bottom bar
            DrawBaseInfo(sb, ViewWidth, ViewHeight);

            sb.End();
        }

        void DrawScoreUI(SpriteBatch sb, Texture2D pixel, SpriteFont font)
        {
            scorePulse += 0.05f;
            if (scorePulse > MathHelper.TwoPi) scorePulse -= MathHelper.TwoPi;

            int score = Player.TotalScore;
            string scoreText = $"Score: {score:N0}";
            Vector2 scoreSize = font.MeasureString(scoreText);
            Vector2 scorePos = new Vector2(15, 15);

            int padding = 12;
            Rectangle scoreBg = new Rectangle((int)scorePos.X - padding, (int)scorePos.Y - padding,
                (int)scoreSize.X + padding * 2, (int)scoreSize.Y + padding * 2);

            Rectangle shadowRect = new Rectangle(scoreBg.X + 3, scoreBg.Y + 3, scoreBg.Width, scoreBg.Height);
            sb.Draw(pixel, shadowRect, new Color(0, 0, 0, 100));

            Color bgColor1 = new Color(20, 30, 60, 220);
            Color bgColor2 = new Color(40, 20, 60, 220);
            for (int i = 0; i < scoreBg.Height; i++)
            {
                float t = (float)i / scoreBg.Height;
                Color gradientColor = Color.Lerp(bgColor1, bgColor2, t);
                sb.Draw(pixel, new Rectangle(scoreBg.X, scoreBg.Y + i, scoreBg.Width, 1), gradientColor);
            }

            float glow = (float)(Math.Sin(scorePulse) * 0.3 + 0.7);
            Color borderColor = Color.Lerp(new Color(100, 150, 255), new Color(150, 100, 255), glow);
            sb.Draw(pixel, new Rectangle(scoreBg.X, scoreBg.Y, scoreBg.Width, 2), borderColor);
            sb.Draw(pixel, new Rectangle(scoreBg.X, scoreBg.Y + scoreBg.Height - 2, scoreBg.Width, 2), borderColor);
            sb.Draw(pixel, new Rectangle(scoreBg.X, scoreBg.Y, 2, scoreBg.Height), borderColor);
            sb.Draw(pixel, new Rectangle(scoreBg.X + scoreBg.Width - 2, scoreBg.Y, 2, scoreBg.Height), borderColor);

            Color textColor = Color.Lerp(Color.White, new Color(200, 220, 255), (float)Math.Sin(scorePulse) * 0.3f);
            sb.DrawString(font, scoreText, scorePos, textColor);
        }

        void DrawPowerUpIndicators(SpriteBatch sb, Texture2D pixel, SpriteFont font)
        {
            float yPos = 70;
            float xPos = 15;
            float spacing = 35;
            int index = 0;

            if (Player.HasJetpack) { DrawPowerUpIcon(sb, pixel, font, xPos, yPos + index * spacing, "JET", Color.Blue); index++; }
            if (Player.HasShield) { DrawPowerUpIcon(sb, pixel, font, xPos, yPos + index * spacing, "SHIELD", Color.Cyan); index++; }
            if (Player.HasSlowMotion) { DrawPowerUpIcon(sb, pixel, font, xPos, yPos + index * spacing, "SLOW", Color.Yellow); index++; }
            if (Player.HasInvincibility) { DrawPowerUpIcon(sb, pixel, font, xPos, yPos + index * spacing, "INV", Color.Purple); index++; }
            if (Player.HasSpeedBoost) { DrawPowerUpIcon(sb, pixel, font, xPos, yPos + index * spacing, "SPEED", Color.Orange); index++; }
            if (Player.HasDoubleJump) { DrawPowerUpIcon(sb, pixel, font, xPos, yPos + index * spacing, "2X JUMP", Color.Green); index++; }
        }

        void DrawPowerUpIcon(SpriteBatch sb, Texture2D pixel, SpriteFont font, float x, float y, string text, Color color)
        {
            float progress = Player.powerUpTimer / Math.Max(0.0001f, Player.powerUpDuration);
            int iconSize = 28;

            Rectangle iconBg = new Rectangle((int)x, (int)y, iconSize, iconSize);
            sb.Draw(pixel, iconBg, new Color(0, 0, 0, 180));

            float pulse = (float)(Math.Sin(progress * 20) * 0.3 + 0.7);
            Color iconColor = Color.Lerp(color, Color.White, pulse * 0.5f);
            Rectangle iconRect = new Rectangle((int)x + 2, (int)y + 2, iconSize - 4, iconSize - 4);
            sb.Draw(pixel, iconRect, iconColor);

            int barWidth = iconSize - 4;
            int barHeight = 3;
            Rectangle progressBg = new Rectangle((int)x + 2, (int)(y + iconSize - 5), barWidth, barHeight);
            sb.Draw(pixel, progressBg, new Color(50, 50, 50, 200));
            Rectangle progressBar = new Rectangle((int)x + 2, (int)(y + iconSize - 5), (int)(barWidth * progress), barHeight);
            sb.Draw(pixel, progressBar, color);

            Vector2 textPos = new Vector2(x + iconSize + 5, y + 5);
            sb.DrawString(font, text, textPos, Color.White);
        }

        void DrawComboUI(SpriteBatch sb, Texture2D pixel, SpriteFont font)
        {
            if (Player.Combo.CurrentCombo > 0)
            {
                string comboText = $"COMBO x{Player.Combo.CurrentCombo}!";
                Vector2 comboSize = font.MeasureString(comboText);
                Vector2 comboPos = new Vector2(ViewWidth - comboSize.X - 15, 15);

                Rectangle comboBg = new Rectangle((int)comboPos.X - 10, (int)comboPos.Y - 5,
                    (int)comboSize.X + 20, (int)comboSize.Y + 10);

                Color bg1 = new Color(255, 200, 0, 200);
                Color bg2 = new Color(255, 100, 0, 200);
                for (int i = 0; i < comboBg.Height; i++)
                {
                    float t = (float)i / comboBg.Height;
                    Color gradColor = Color.Lerp(bg1, bg2, t);
                    sb.Draw(pixel, new Rectangle(comboBg.X, comboBg.Y + i, comboBg.Width, 1), gradColor);
                }

                Color comboColor = Player.Combo.CurrentCombo >= 10 ? Color.Gold :
                                  Player.Combo.CurrentCombo >= 5 ? Color.Orange : Color.Yellow;
                sb.DrawString(font, comboText, comboPos, comboColor);
            }
        }

        void DrawSpecialEventUI(SpriteBatch sb, Texture2D pixel, SpriteFont font)
        {
            if (specialEvent.IsActive)
            {
                string eventText = specialEvent.Type == SpecialEventType.MeteorShower ? "METEOR SHOWER!" :
                                  specialEvent.Type == SpecialEventType.Wind ? "WIND STORM!" : "";

                if (!string.IsNullOrEmpty(eventText))
                {
                    Vector2 eventSize = font.MeasureString(eventText);
                    Vector2 eventPos = new Vector2((ViewWidth - eventSize.X) / 2, 100);

                    float pulse = (float)(Math.Sin(Player.gameTime * 5) * 0.3 + 0.7);
                    Color eventColor = specialEvent.Type == SpecialEventType.MeteorShower ?
                        Color.Lerp(Color.Orange, Color.Red, pulse) :
                        Color.Lerp(Color.Cyan, Color.Blue, pulse);

                    sb.DrawString(font, eventText, eventPos + new Vector2(2, 2), new Color(0, 0, 0, 150));
                    sb.DrawString(font, eventText, eventPos, eventColor);
                }
            }
        }

        public void GameOver()
        {
            IsGameOver = true;
        }
    }

    // ================= HIGHSCORE =================
    public class HighScoreEntry
    {
        public string Name;
        public int Score;
    }

    public class HighScoreManager
    {
        string file;
        public List<HighScoreEntry> Scores = new List<HighScoreEntry>();
        public int MaxEntries = 10;

        public HighScoreManager(string path)
        {
            file = path;
            Load();
        }

        public void AddScore(string name, int score)
        {
            Scores.Add(new HighScoreEntry { Name = name, Score = score });
            Scores = Scores.OrderByDescending(s => s.Score).Take(MaxEntries).ToList();
            Save();
        }

        void Load()
        {
            Scores.Clear();
            if (!File.Exists(file)) return;

            foreach (var line in File.ReadAllLines(file))
            {
                var parts = line.Split(';');
                if (parts.Length != 2) continue;

                if (int.TryParse(parts[1], out int sc))
                    Scores.Add(new HighScoreEntry { Name = parts[0], Score = sc });
            }

            Scores = Scores.OrderByDescending(s => s.Score).Take(MaxEntries).ToList();
        }

        void Save()
        {
            try
            {
                var lines = Scores.Select(s => $"{s.Name};{s.Score}");
                File.WriteAllLines(file, lines);
            }
            catch
            {
                // ignore IO errors in school environments
            }
        }
    }

    // ================= ENEMY =================
    public class Enemy : GameObject
    {
        Texture2D texture;
        float moveSpeed;
        float moveRange;
        float startX;
        bool movingRight = true;

        public Enemy(float x, float y, float speed, float range)
        {
            Position = new Vector2(x, y);
            Size = new Vector2(32, 32);
            moveSpeed = speed;
            moveRange = range;
            startX = x;
            texture = CreateEnemyTexture();
        }

        Texture2D CreateEnemyTexture()
        {
            Texture2D tex = new Texture2D(Game1.Instance.GraphicsDevice, 32, 32);
            Color[] data = new Color[32 * 32];
            for (int i = 0; i < data.Length; i++)
            {
                int x = i % 32;
                int y = i / 32;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
                if (dist < 14) data[i] = Color.Red;
                else if (dist < 15) data[i] = Color.DarkRed;
                else data[i] = Color.Transparent;
            }
            tex.SetData(data);
            return tex;
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;

            if (movingRight)
            {
                Position.X += moveSpeed * dt;
                if (Position.X >= startX + moveRange) { Position.X = startX + moveRange; movingRight = false; }
            }
            else
            {
                Position.X -= moveSpeed * dt;
                if (Position.X <= startX) { Position.X = startX; movingRight = true; }
            }
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel) => sb.Draw(texture, Bounds, Color.White);

        public virtual bool CheckCollision(Player player) => Bounds.Intersects(player.Bounds);
    }

    // ================= OBSTACLE =================
    public class Obstacle : GameObject
    {
        Texture2D texture;

        public Obstacle(float x, float y)
        {
            Position = new Vector2(x, y);
            Size = new Vector2(40, 40);
            texture = CreateObstacleTexture();
        }

        Texture2D CreateObstacleTexture()
        {
            Texture2D tex = new Texture2D(Game1.Instance.GraphicsDevice, 40, 40);
            Color[] data = new Color[40 * 40];
            for (int i = 0; i < data.Length; i++)
            {
                int x = i % 40;
                int y = i / 40;
                data[i] = (x < 5 || x >= 35 || y < 5 || y >= 35) ? Color.DarkGray : Color.Gray;
            }
            tex.SetData(data);
            return tex;
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel) => sb.Draw(texture, Bounds, Color.White);
        public bool CheckCollision(Player player) => Bounds.Intersects(player.Bounds);
    }

    // ================= BOMB (spawns on landed platform) =================
    public class Bomb : GameObject
    {
        public Platform Parent { get; private set; }
        float armTimer = 0.25f; // small grace period
        float pulse = 0f;

        public bool IsArmed => armTimer <= 0f;

        public Bomb(Platform parent)
        {
            Parent = parent;
            Size = new Vector2(28, 28);
            UpdatePositionFromParent();
        }

        void UpdatePositionFromParent()
        {
            if (Parent == null) return;
            Position = new Vector2(
                Parent.Position.X + Parent.Size.X / 2f - Size.X / 2f,
                Parent.Position.Y - Size.Y + 2f
            );
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;
            armTimer -= dt;
            pulse += dt * 8f;
            if (pulse > MathHelper.TwoPi) pulse -= MathHelper.TwoPi;

            // Follow platform (moving platforms)
            UpdatePositionFromParent();
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            var tex = Game1.BombTexture ?? pixel;
            float a = IsArmed ? (float)(Math.Sin(pulse) * 0.25 + 0.75) : 0.65f;
            sb.Draw(tex, Bounds, Color.White * a);
        }

        public bool CheckCollision(Player player)
        {
            return Bounds.Intersects(player.Bounds);
        }
    }

    // ================= POWER-UP =================
    public class PowerUp : GameObject
    {
        public enum PowerUpType { Jetpack, Shield, SlowMotion, Invincibility, SpeedBoost, DoubleJump }
        public PowerUpType Type;
        Texture2D texture;
        float rotation = 0f;
        float bobOffset = 0f;

        public PowerUp(float x, float y, PowerUpType type)
        {
            Position = new Vector2(x, y);
            Size = new Vector2(32, 32);
            Type = type;
            texture = CreatePowerUpTexture();
        }

        Texture2D CreatePowerUpTexture()
        {
            Texture2D tex = new Texture2D(Game1.Instance.GraphicsDevice, 32, 32);
            Color[] data = new Color[32 * 32];
            Color color = Type == PowerUpType.Jetpack ? Color.Blue :
                         Type == PowerUpType.Shield ? Color.Cyan :
                         Type == PowerUpType.SlowMotion ? Color.Yellow :
                         Type == PowerUpType.Invincibility ? Color.Purple :
                         Type == PowerUpType.SpeedBoost ? Color.Orange :
                         Color.Green;

            for (int i = 0; i < data.Length; i++)
            {
                int x = i % 32;
                int y = i / 32;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
                if (dist < 12) data[i] = color;
                else if (dist < 14) data[i] = Color.Lerp(color, Color.White, 0.5f);
                else data[i] = Color.Transparent;
            }
            tex.SetData(data);
            return tex;
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            rotation += 2f * (float)gt.ElapsedGameTime.TotalSeconds;
            bobOffset = (float)Math.Sin(rotation * 2) * 3f;
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            Vector2 drawPos = new Vector2(Position.X, Position.Y + bobOffset);
            Rectangle drawRect = new Rectangle((int)drawPos.X, (int)drawPos.Y, (int)Size.X, (int)Size.Y);
            sb.Draw(texture, drawRect, null, Color.White, rotation, new Vector2(16, 16), SpriteEffects.None, 0f);
        }

        public bool CheckCollision(Player player)
        {
            Rectangle checkRect = new Rectangle((int)Position.X, (int)(Position.Y + bobOffset), (int)Size.X, (int)Size.Y);
            return checkRect.Intersects(player.Bounds);
        }
    }

    // ================= PLAYER ANIMATION =================
    public class PlayerAnimation
    {
        public enum AnimationState { Idle, Moving, Jumping, Falling }
        public AnimationState CurrentState = AnimationState.Idle;
        float animationTimer = 0f;
        int currentFrame = 0;
        float frameTime = 0.1f;

        public void Update(GameTime gt, Vector2 velocity)
        {
            animationTimer += (float)gt.ElapsedGameTime.TotalSeconds;

            if (Math.Abs(velocity.X) > 10) CurrentState = AnimationState.Moving;
            else if (velocity.Y < -50) CurrentState = AnimationState.Jumping;
            else if (velocity.Y > 50) CurrentState = AnimationState.Falling;
            else CurrentState = AnimationState.Idle;

            if (animationTimer >= frameTime)
            {
                animationTimer = 0f;
                currentFrame = (currentFrame + 1) % 4;
            }
        }

        public float GetRotation(Vector2 velocity)
        {
            if (Math.Abs(velocity.X) > 10)
                return velocity.X > 0 ? 0.1f : -0.1f;
            return 0f;
        }

        public Color GetTint() => Color.White;
    }

    // ================= PARALLAX BACKGROUND =================
    public class ParallaxBackground
    {
        List<ParallaxLayer> layers = new List<ParallaxLayer>();
        float cameraY = 0f;

        public ParallaxBackground(GraphicsDevice device)
        {
            layers.Add(new ParallaxLayer(device, 480, 2000, Color.White, 0.1f, 50));
            layers.Add(new ParallaxLayer(device, 480, 2000, Color.LightBlue, 0.2f, 20));
            layers.Add(new ParallaxLayer(device, 480, 2000, Color.LightGray, 0.4f, 15));
        }

        public void Update(float cameraY)
        {
            this.cameraY = cameraY;
            foreach (var layer in layers)
                layer.Update(cameraY);
        }

        public void Draw(SpriteBatch sb)
        {
            foreach (var layer in layers)
                layer.Draw(sb, cameraY);
        }
    }

    public class ParallaxLayer
    {
        Texture2D texture;
        List<Vector2> elements = new List<Vector2>();
        float parallaxSpeed;
        Color color;
        int elementSize;
        Random rnd = Game1.Rng;
        int viewHeight;

        public ParallaxLayer(GraphicsDevice device, int width, int height, Color elementColor, float speed, int size)
        {
            texture = CreateElementTexture(device, size, elementColor);
            parallaxSpeed = speed;
            color = elementColor;
            elementSize = size;
            viewHeight = 800;

            int count = Math.Max(1, (int)(height / (size * 2)));
            for (int i = 0; i < count; i++)
                elements.Add(new Vector2(rnd.Next(0, width), rnd.Next(0, height)));
        }

        Texture2D CreateElementTexture(GraphicsDevice device, int size, Color col)
        {
            Texture2D tex = new Texture2D(device, size, size);
            Color[] data = new Color[size * size];

            for (int i = 0; i < data.Length; i++)
            {
                int x = i % size;
                int y = i / size;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2, size / 2));

                if (dist < size / 2 - 2) data[i] = col;
                else if (dist < size / 2) data[i] = Color.Lerp(col, Color.Transparent, 0.5f);
                else data[i] = Color.Transparent;
            }

            tex.SetData(data);
            return tex;
        }

        public void Update(float cameraY)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                var elem = elements[i];
                float worldY = elem.Y - cameraY * parallaxSpeed;

                if (worldY > viewHeight + 100)
                    elements[i] = new Vector2(elem.X, cameraY - viewHeight - 100);
                else if (worldY < -100)
                    elements[i] = new Vector2(elem.X, cameraY + viewHeight + 100);
            }
        }

        public void Draw(SpriteBatch sb, float cameraY)
        {
            foreach (var elem in elements)
            {
                float screenY = elem.Y - cameraY * parallaxSpeed;
                if (screenY >= -elementSize && screenY <= viewHeight + elementSize)
                    sb.Draw(texture, new Vector2(elem.X, screenY), color);
            }
        }
    }

    // ================= FLYING ENEMY =================
    public class FlyingEnemy : Enemy
    {
        float verticalSpeed;
        float verticalRange;
        float startY;
        bool movingUp = true;

        public FlyingEnemy(float x, float y, float hSpeed, float hRange, float vSpeed, float vRange)
            : base(x, y, hSpeed, hRange)
        {
            verticalSpeed = vSpeed;
            verticalRange = vRange;
            startY = y;
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            base.Update(gt, screen);
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;

            if (movingUp)
            {
                Position.Y -= verticalSpeed * dt;
                if (Position.Y <= startY - verticalRange) { Position.Y = startY - verticalRange; movingUp = false; }
            }
            else
            {
                Position.Y += verticalSpeed * dt;
                if (Position.Y >= startY + verticalRange) { Position.Y = startY + verticalRange; movingUp = true; }
            }
        }
    }

    // ================= BULLET =================
    public class Bullet : GameObject
    {
        Vector2 velocity;
        bool isEnemyBullet;

        public Bullet(float x, float y, float bulletSpeed, bool enemyBullet)
        {
            Position = new Vector2(x, y);
            Size = new Vector2(8, 16);
            velocity = new Vector2(0, bulletSpeed);
            isEnemyBullet = enemyBullet;
        }

        public Bullet(float x, float y, float velX, float velY, bool enemyBullet)
        {
            Position = new Vector2(x, y);
            Size = new Vector2(8, 16);
            velocity = new Vector2(velX, velY);
            isEnemyBullet = enemyBullet;
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;
            Position += velocity * dt;
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            Color color = isEnemyBullet ? Color.Red : Color.Yellow;
            sb.Draw(pixel, Bounds, color);
        }

        public bool CheckCollision(Player player) => Bounds.Intersects(player.Bounds);
    }

    // ================= SHOOTING ENEMY =================
    public class ShootingEnemy : Enemy
    {
        public List<Bullet> Bullets = new List<Bullet>();
        float shootTimer = 0f;
        float shootInterval = 2f;

        public ShootingEnemy(float x, float y, float speed, float range) : base(x, y, speed, range) { }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            base.Update(gt, screen);

            float dt = (float)gt.ElapsedGameTime.TotalSeconds;
            shootTimer += dt;

            if (shootTimer >= shootInterval)
            {
                shootTimer = 0f;
                Bullets.Add(new Bullet(Position.X + Size.X / 2, Position.Y + Size.Y, 200f, true));
            }

            foreach (var bullet in Bullets.ToList())
            {
                bullet.Update(gt, screen);
                if (bullet.Position.Y > screen.CameraY + screen.ViewHeight + 200)
                    Bullets.Remove(bullet);
            }
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            base.Draw(sb, pixel);
            foreach (var bullet in Bullets)
                bullet.Draw(sb, pixel);
        }
    }

    // ================= CHASING ENEMY =================
    public class ChasingEnemy : Enemy
    {
        float chaseSpeed = 100f;
        Player targetPlayer;

        public ChasingEnemy(float x, float y, float speed, float range) : base(x, y, speed, range) { }

        public void SetTarget(Player player) => targetPlayer = player;

        public override void Update(GameTime gt, PlayScreen screen)
        {
            if (targetPlayer != null)
            {
                float dt = (float)gt.ElapsedGameTime.TotalSeconds;

                Vector2 direction = targetPlayer.Position - Position;
                if (direction.Length() > 0)
                {
                    direction.Normalize();
                    Position += direction * chaseSpeed * dt;
                }
            }
            else base.Update(gt, screen);
        }
    }

    // ================= BLACK HOLE =================
    public class BlackHole : Enemy
    {
        float pullStrength = 200f;
        float pullRadius = 150f;
        float rotationSpeed = 1.5f;
        float rotation = 0f;
        float pulse = 0f;
        Texture2D holeTexture;
        Texture2D paperTexture;

        public BlackHole(float x, float y) : base(x, y, 0, 0)
        {
            Size = new Vector2(80, 80);
            holeTexture = CreateBlackHoleTexture();
            paperTexture = CreatePaperTexture();
        }

        Texture2D CreateBlackHoleTexture()
        {
            Texture2D tex = new Texture2D(Game1.Instance.GraphicsDevice, 80, 80);
            Color[] data = new Color[80 * 80];

            for (int i = 0; i < data.Length; i++)
            {
                int x = i % 80;
                int y = i / 80;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(40, 40));

                if (dist < 35) data[i] = Color.Black;
                else if (dist < 40)
                {
                    float t = (dist - 35) / 5f;
                    data[i] = Color.Lerp(Color.Black, new Color(20, 20, 20), t);
                }
                else data[i] = Color.Transparent;
            }

            tex.SetData(data);
            return tex;
        }

        Texture2D CreatePaperTexture()
        {
            Texture2D tex = new Texture2D(Game1.Instance.GraphicsDevice, 80, 80);
            Color[] data = new Color[80 * 80];

            Color paperColor = new Color(250, 245, 230);

            for (int i = 0; i < data.Length; i++)
            {
                int x = i % 80;
                int y = i / 80;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(40, 40));

                if (dist > 40)
                {
                    bool isGridLine = (x % 8 == 0) || (y % 8 == 0);
                    data[i] = isGridLine ? Color.Lerp(paperColor, Color.Black, 0.1f) : paperColor;
                }
                else if (dist > 35)
                {
                    float angle = (float)Math.Atan2(y - 40, x - 40);
                    float noise = (float)(Math.Sin(angle * 8) * 2 + Math.Cos(angle * 6) * 1.5);
                    float edgeDist = dist - 35 + noise;

                    if (edgeDist > 0 && edgeDist < 3) data[i] = Color.Lerp(paperColor, Color.White, 0.7f);
                    else if (edgeDist < 0) data[i] = Color.Lerp(paperColor, Color.Black, 0.3f);
                    else data[i] = paperColor;
                }
                else data[i] = Color.Transparent;
            }

            tex.SetData(data);
            return tex;
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;
            rotation += rotationSpeed * dt;
            pulse += dt * 2f;
            if (pulse > MathHelper.TwoPi) pulse -= MathHelper.TwoPi;

            Vector2 toPlayer = screen.Player.Position - Position;
            float distance = toPlayer.Length();

            if (distance < pullRadius && distance > 0)
            {
                float pullForce = pullStrength * (1f - distance / pullRadius);
                toPlayer.Normalize();
                screen.Player.Velocity += toPlayer * pullForce * dt;

                if (distance < Size.X / 2)
                    screen.GameOver();
            }
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            float scale = 1f + (float)(Math.Sin(pulse) * 0.1f);
            Vector2 center = new Vector2(Position.X + Size.X / 2, Position.Y + Size.Y / 2);
            Vector2 origin = new Vector2(40, 40);

            Rectangle paperRect = new Rectangle((int)Position.X, (int)Position.Y, (int)(Size.X * scale), (int)(Size.Y * scale));
            sb.Draw(paperTexture, paperRect, null, Color.White, rotation, origin, SpriteEffects.None, 0f);

            float holeScale = 1f + (float)(Math.Sin(pulse * 1.5f) * 0.05f);
            Rectangle holeRect = new Rectangle(
                (int)(Position.X + (Size.X - Size.X * holeScale) / 2),
                (int)(Position.Y + (Size.Y - Size.Y * holeScale) / 2),
                (int)(Size.X * holeScale),
                (int)(Size.Y * holeScale)
            );
            sb.Draw(holeTexture, holeRect, null, Color.White, -rotation * 0.5f, origin, SpriteEffects.None, 0f);

            float glowAlpha = (float)(Math.Sin(pulse * 2) * 0.3 + 0.2);
            for (int i = 0; i < 3; i++)
            {
                float glowRadius = 40 + i * 5;
                Rectangle glowRect = new Rectangle((int)(center.X - glowRadius), (int)(center.Y - glowRadius), (int)(glowRadius * 2), (int)(glowRadius * 2));
                Color glowColor = new Color(50, 0, 100, (int)(glowAlpha * 255 / (i + 1)));
                sb.Draw(pixel, glowRect, glowColor);
            }
        }

        public override bool CheckCollision(Player player) => false; // handled by pull
    }

    // ================= COIN =================
    public class Coin : GameObject
    {
        Texture2D texture;
        float rotation = 0f;
        float bobOffset = 0f;

        public Coin(float x, float y)
        {
            Position = new Vector2(x, y);
            Size = new Vector2(24, 24);
            texture = CreateCoinTexture();
        }

        Texture2D CreateCoinTexture()
        {
            Texture2D tex = new Texture2D(Game1.Instance.GraphicsDevice, 24, 24);
            Color[] data = new Color[24 * 24];

            for (int i = 0; i < data.Length; i++)
            {
                int x = i % 24;
                int y = i / 24;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(12, 12));

                if (dist < 10) data[i] = Color.Gold;
                else if (dist < 11) data[i] = Color.Yellow;
                else data[i] = Color.Transparent;
            }

            tex.SetData(data);
            return tex;
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            rotation += 3f * (float)gt.ElapsedGameTime.TotalSeconds;
            bobOffset = (float)Math.Sin(rotation * 2) * 5f;
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            Vector2 drawPos = new Vector2(Position.X, Position.Y + bobOffset);
            Rectangle drawRect = new Rectangle((int)drawPos.X, (int)drawPos.Y, (int)Size.X, (int)Size.Y);
            sb.Draw(texture, drawRect, null, Color.White, rotation, new Vector2(12, 12), SpriteEffects.None, 0f);
        }

        public bool CheckCollision(Player player)
        {
            Rectangle checkRect = new Rectangle((int)Position.X, (int)(Position.Y + bobOffset), (int)Size.X, (int)Size.Y);
            return checkRect.Intersects(player.Bounds);
        }
    }

    // ================= PARTICLE SYSTEM =================
    public class Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Color Color;
        public float Life;
        public float MaxLife;
        public float Size;
        public float Rotation;
        public float RotationSpeed;
        public bool IsAlive => Life > 0;
    }

    public class ParticleSystem
    {
        List<Particle> particles = new List<Particle>();
        Texture2D pixel;

        public ParticleSystem(Texture2D pixelTexture) => pixel = pixelTexture;

        public void CreateJumpEffect(Vector2 position, Color color, int count = 8)
        {
            var rnd = Game1.Rng;
            for (int i = 0; i < count; i++)
            {
                float angle = (float)(rnd.NextDouble() * Math.PI * 2);
                float speed = rnd.Next(30, 80);
                particles.Add(new Particle
                {
                    Position = position,
                    Velocity = new Vector2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed - 50),
                    Color = color,
                    Life = 0.5f,
                    MaxLife = 0.5f,
                    Size = rnd.Next(3, 6),
                    Rotation = (float)(rnd.NextDouble() * Math.PI * 2),
                    RotationSpeed = (float)(rnd.NextDouble() * 5 - 2.5)
                });
            }
        }

        public void CreateCollectEffect(Vector2 position, Color color, int count = 15)
        {
            var rnd = Game1.Rng;
            for (int i = 0; i < count; i++)
            {
                float angle = (float)(rnd.NextDouble() * Math.PI * 2);
                float speed = rnd.Next(50, 120);
                particles.Add(new Particle
                {
                    Position = position,
                    Velocity = new Vector2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed),
                    Color = color,
                    Life = 0.8f,
                    MaxLife = 0.8f,
                    Size = rnd.Next(4, 8),
                    Rotation = (float)(rnd.NextDouble() * Math.PI * 2),
                    RotationSpeed = (float)(rnd.NextDouble() * 8 - 4)
                });
            }
        }

        public void CreateComboEffect(Vector2 position, int comboCount)
        {
            var rnd = Game1.Rng;
            Color comboColor = comboCount >= 10 ? Color.Gold : comboCount >= 5 ? Color.Orange : Color.Yellow;

            for (int i = 0; i < comboCount * 2; i++)
            {
                float angle = (float)(rnd.NextDouble() * Math.PI * 2);
                float speed = rnd.Next(40, 100);
                particles.Add(new Particle
                {
                    Position = position,
                    Velocity = new Vector2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed - 30),
                    Color = comboColor,
                    Life = 1.0f,
                    MaxLife = 1.0f,
                    Size = rnd.Next(5, 10),
                    Rotation = (float)(rnd.NextDouble() * Math.PI * 2),
                    RotationSpeed = (float)(rnd.NextDouble() * 10 - 5)
                });
            }
        }

        public void Update(GameTime gt)
        {
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;

            foreach (var particle in particles.ToList())
            {
                particle.Life -= dt;
                particle.Position += particle.Velocity * dt;
                particle.Velocity.Y += 200f * dt;
                particle.Rotation += particle.RotationSpeed * dt;

                if (!particle.IsAlive)
                    particles.Remove(particle);
            }
        }

        public void Draw(SpriteBatch sb, float cameraY)
        {
            foreach (var particle in particles)
            {
                if (particle.Position.Y >= cameraY - 100 && particle.Position.Y <= cameraY + 1000)
                {
                    float alpha = particle.Life / particle.MaxLife;
                    Color drawColor = particle.Color * alpha;

                    Rectangle particleRect = new Rectangle(
                        (int)(particle.Position.X - particle.Size / 2),
                        (int)(particle.Position.Y - particle.Size / 2),
                        (int)particle.Size,
                        (int)particle.Size
                    );

                    sb.Draw(pixel, particleRect, drawColor);
                }
            }
        }
    }

    // ================= COMBO SYSTEM =================
    public class ComboSystem
    {
        public int CurrentCombo = 0;
        public int MaxCombo = 0;
        float comboTimer = 0f;
        float comboTimeLimit = 2f;
        float lastJumpTime = 0f;

        public void OnJump(float currentTime)
        {
            if (currentTime - lastJumpTime < comboTimeLimit)
            {
                CurrentCombo++;
                if (CurrentCombo > MaxCombo) MaxCombo = CurrentCombo;
            }
            else CurrentCombo = 1;

            lastJumpTime = currentTime;
            comboTimer = comboTimeLimit;
        }

        public void Update(GameTime gt)
        {
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;
            comboTimer -= dt;
            if (comboTimer <= 0) CurrentCombo = 0;
        }

        public int GetBonusScore()
        {
            if (CurrentCombo >= 10) return CurrentCombo * 5;
            if (CurrentCombo >= 5) return CurrentCombo * 3;
            if (CurrentCombo >= 3) return CurrentCombo * 2;
            return 0;
        }
    }

    // ================= SPECIAL EVENTS =================
    public enum SpecialEventType { None, MeteorShower, Wind }

    public class SpecialEvent
    {
        public SpecialEventType Type = SpecialEventType.None;
        public float Duration = 0f;
        public float Timer = 0f;
        Random rnd = Game1.Rng;

        public void Start(SpecialEventType type, float duration)
        {
            Type = type;
            Duration = duration;
            Timer = duration;
        }

        public void Update(GameTime gt, PlayScreen screen)
        {
            if (Type == SpecialEventType.None) return;

            float dt = (float)gt.ElapsedGameTime.TotalSeconds;
            Timer -= dt;

            if (Type == SpecialEventType.MeteorShower)
            {
                if (rnd.Next(0, 100) < 15)
                {
                    float spawnX = rnd.Next(0, screen.ViewWidth);
                    float spawnY = screen.Player.Position.Y - screen.ViewHeight;
                    screen.Enemies.Add(new Meteor(spawnX, spawnY));
                }
            }

            if (Timer <= 0)
                Type = SpecialEventType.None;
        }

        public bool IsActive => Type != SpecialEventType.None && Timer > 0;
    }

    // ================= METEOR =================
    public class Meteor : Enemy
    {
        float fallSpeed = 300f;
        float rotation = 0f;
        Texture2D meteorTexture;

        public Meteor(float x, float y) : base(x, y, 0, 0)
        {
            Size = new Vector2(40, 40);
            meteorTexture = CreateMeteorTexture();
        }

        Texture2D CreateMeteorTexture()
        {
            Texture2D tex = new Texture2D(Game1.Instance.GraphicsDevice, 40, 40);
            Color[] data = new Color[40 * 40];

            for (int i = 0; i < data.Length; i++)
            {
                int x = i % 40;
                int y = i / 40;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(20, 20));

                if (dist < 18)
                {
                    float t = dist / 18f;
                    data[i] = Color.Lerp(Color.Orange, Color.DarkRed, t);
                }
                else if (dist < 20) data[i] = Color.Yellow;
                else data[i] = Color.Transparent;
            }

            tex.SetData(data);
            return tex;
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;
            Position.Y += fallSpeed * dt;
            rotation += 3f * dt;
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            Rectangle meteorRect = new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y);
            sb.Draw(meteorTexture, meteorRect, null, Color.White, rotation, new Vector2(20, 20), SpriteEffects.None, 0f);

            for (int i = 0; i < 3; i++)
            {
                Rectangle trailRect = new Rectangle((int)Position.X, (int)(Position.Y + Size.Y + i * 10), (int)Size.X, 8);
                Color trailColor = new Color(255, 200, 0, 150 - i * 50);
                sb.Draw(pixel, trailRect, trailColor);
            }
        }
    }

    // ================= BOSS =================
    public class Boss : Enemy
    {
        public int Health = 5;
        public int MaxHealth = 5;
        float moveSpeed;
        float moveRange;
        float startX;
        bool movingRight = true;
        float attackTimer = 0f;
        float attackInterval = 3f;
        public List<Bullet> Bullets = new List<Bullet>();
        Texture2D bossTexture;
        float rotation = 0f;
        float pulse = 0f;

        public Boss(float x, float y, float speed, float range) : base(x, y, speed, range)
        {
            Size = new Vector2(120, 120);
            moveSpeed = speed;
            moveRange = range;
            startX = x;
            bossTexture = CreateBossTexture();
        }

        Texture2D CreateBossTexture()
        {
            Texture2D tex = new Texture2D(Game1.Instance.GraphicsDevice, 120, 120);
            Color[] data = new Color[120 * 120];

            for (int i = 0; i < data.Length; i++)
            {
                int x = i % 120;
                int y = i / 120;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(60, 60));

                if (dist < 50)
                {
                    float t = dist / 50f;
                    data[i] = Color.Lerp(new Color(200, 0, 0), new Color(100, 0, 0), t);
                }
                else if ((x >= 40 && x <= 50 && y >= 45 && y <= 55) ||
                         (x >= 70 && x <= 80 && y >= 45 && y <= 55))
                {
                    data[i] = Color.Yellow;
                }
                else if (x >= 50 && x <= 70 && y >= 70 && y <= 80)
                {
                    data[i] = Color.DarkRed;
                }
                else if (dist >= 50 && dist < 55)
                {
                    data[i] = new Color(255, 100, 0, 200);
                }
                else data[i] = Color.Transparent;
            }

            tex.SetData(data);
            return tex;
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            if (Health <= 0) return;

            float dt = (float)gt.ElapsedGameTime.TotalSeconds;
            rotation += 0.5f * dt;
            pulse += dt * 3f;
            if (pulse > MathHelper.TwoPi) pulse -= MathHelper.TwoPi;

            if (movingRight)
            {
                Position.X += moveSpeed * dt;
                if (Position.X >= startX + moveRange) { Position.X = startX + moveRange; movingRight = false; }
            }
            else
            {
                Position.X -= moveSpeed * dt;
                if (Position.X <= startX) { Position.X = startX; movingRight = true; }
            }

            attackTimer += dt;
            if (attackTimer >= attackInterval)
            {
                attackTimer = 0f;
                Vector2 toPlayer = screen.Player.Position - Position;
                if (toPlayer.Length() > 0)
                {
                    toPlayer.Normalize();
                    Bullets.Add(new Bullet(
                        Position.X + Size.X / 2,
                        Position.Y + Size.Y / 2,
                        150f * toPlayer.X,
                        200f * toPlayer.Y,
                        true
                    ));
                }
            }

            foreach (var bullet in Bullets.ToList())
            {
                bullet.Update(gt, screen);
                if (bullet.Position.Y > screen.CameraY + screen.ViewHeight + 200 ||
                    bullet.Position.Y < screen.CameraY - 200 ||
                    bullet.Position.X < -50 || bullet.Position.X > screen.ViewWidth + 50)
                    Bullets.Remove(bullet);
            }
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            if (Health <= 0) return;

            float scale = 1f + (float)(Math.Sin(pulse) * 0.1f);
            Vector2 center = new Vector2(Position.X + Size.X / 2, Position.Y + Size.Y / 2);

            Rectangle bossRect = new Rectangle(
                (int)(Position.X + (Size.X - Size.X * scale) / 2),
                (int)(Position.Y + (Size.Y - Size.Y * scale) / 2),
                (int)(Size.X * scale),
                (int)(Size.Y * scale)
            );

            float glowAlpha = (float)(Math.Sin(pulse * 2) * 0.3 + 0.5);
            for (int i = 0; i < 3; i++)
            {
                float glowRadius = 60 + i * 10;
                Rectangle glowRect = new Rectangle((int)(center.X - glowRadius), (int)(center.Y - glowRadius), (int)(glowRadius * 2), (int)(glowRadius * 2));
                Color glowColor = new Color(255, 100, 0, (int)(glowAlpha * 100 / (i + 1)));
                sb.Draw(pixel, glowRect, glowColor);
            }

            sb.Draw(bossTexture, bossRect, null, Color.White, rotation, new Vector2(60, 60), SpriteEffects.None, 0f);

            int barWidth = 100;
            int barHeight = 8;
            Rectangle healthBg = new Rectangle((int)(center.X - barWidth / 2), (int)(Position.Y - 20), barWidth, barHeight);
            sb.Draw(pixel, healthBg, new Color(50, 0, 0, 200));

            float healthPercent = (float)Health / MaxHealth;
            Rectangle healthBar = new Rectangle((int)(center.X - barWidth / 2), (int)(Position.Y - 20), (int)(barWidth * healthPercent), barHeight);

            Color healthColor = healthPercent > 0.5f ? Color.Green :
                               healthPercent > 0.25f ? Color.Yellow : Color.Red;
            sb.Draw(pixel, healthBar, healthColor);

            foreach (var bullet in Bullets)
                bullet.Draw(sb, pixel);
        }

        public override bool CheckCollision(Player player)
        {
            if (Bounds.Intersects(player.Bounds))
            {
                if (!player.HasInvincibility && !player.HasShield)
                    return true;
            }
            return false;
        }

        public bool TakeDamage()
        {
            Health--;
            return Health <= 0;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace DoodleJump
{
    public class Game1 : Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        public Texture2D pixel;
        SpriteFont font;

        public static Texture2D HeroTexture;
        public static Texture2D NormalPlatformTexture;
        public static Texture2D MovingPlatformTexture;
        public static Texture2D DisappearingPlatformTexture;
        public static Texture2D SpringPlatformTexture;

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

            // CREATE PLATFORM TEXTURES (согласно изображению)
            NormalPlatformTexture = CreatePlatformTexture(GraphicsDevice, 80, 16, Color.Green, Color.DarkGreen); // Зеленые обычные
            MovingPlatformTexture = CreatePlatformTexture(GraphicsDevice, 80, 16, Color.Green, Color.DarkGreen); // Движущиеся тоже зеленые
            DisappearingPlatformTexture = CreatePlatformTexture(GraphicsDevice, 80, 16, Color.White, Color.LightGray); // Белые исчезающие
            SpringPlatformTexture = CreatePlatformTexture(GraphicsDevice, 80, 16, Color.Red, Color.DarkRed); // Красные с пружиной

            highScoreManager = new HighScoreManager("highscores.txt");
            screenManager = new ScreenManager(this, pixel, font, highScoreManager);
            screenManager.Setup();
        }

        // Создание текстуры платформы с градиентом
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
                    // Добавляем текстуру (полосы)
                    if (y < 2 || y >= height - 2)
                    {
                        // Верхняя и нижняя границы - темнее
                        data[y * width + x] = Color.Lerp(currentColor, Color.Black, 0.3f);
                    }
                    else if (x < 2 || x >= width - 2)
                    {
                        // Боковые границы - темнее
                        data[y * width + x] = Color.Lerp(currentColor, Color.Black, 0.2f);
                    }
                    else
                    {
                        // Основная часть с легкой текстурой
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
        public float StartY; // Начальная позиция Y
        public float HighestY; // Самая высокая позиция (самое маленькое значение Y)
        public int Score => (int)Math.Max(0, (StartY - HighestY) / 10f) + Combo.GetBonusScore(); // Счет + бонусы за комбо
        
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
        
        // Дополнительные очки от монет
        public int CoinScore = 0;
        
        // Анимация
        PlayerAnimation animation = new PlayerAnimation();
        
        // Для двойного прыжка
        bool wasJumpPressed = false;
        
        // Комбо-система
        public ComboSystem Combo = new ComboSystem();
        public float gameTime = 0f;

        public Player()
        {
            texture = Game1.HeroTexture;
            Size = new Vector2(48, 48);
            Position = new Vector2(220, 700);
            Velocity = Vector2.Zero;
            StartY = Position.Y; // Сохраняем начальную позицию
            HighestY = Position.Y; // Начальная высота
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;
            gameTime += dt;
            Combo.Update(gt);

            var ks = Keyboard.GetState();
            float speed = HasSpeedBoost ? 400f : 250f;
            
            // Эффект ветра
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

            // Обновляем анимацию
            animation.Update(gt, Velocity);
            
            // Power-up эффекты
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
            
            
            // Jetpack эффект
            if (HasJetpack && Velocity.Y > 0)
            {
                Velocity.Y -= 300f * dt; // Медленнее падает
            }
            
            float gravity = HasSlowMotion ? 450f : 900f;
            Velocity.Y += gravity * dt;
            Position += Velocity * dt;

            // Обновляем самую высокую позицию (меньшее значение Y = выше)
            if (Position.Y < HighestY)
                HighestY = Position.Y;

            // Обработка двойного прыжка (пробел в воздухе)
            bool jumpPressed = ks.IsKeyDown(Keys.Space) || ks.IsKeyDown(Keys.Up) || ks.IsKeyDown(Keys.W);
            if (HasDoubleJump && canDoubleJump && jumpPressed && !wasJumpPressed)
            {
                if (jumpCount < 1 && Velocity.Y > 0) // Можно прыгнуть второй раз в воздухе
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
                        
                        // Комбо-система
                        Combo.OnJump(gameTime);
                        
                        // Частицы при прыжке
                        Color jumpColor = p is SpringPlatform ? Color.Orange : Color.LightBlue;
                        screen.particleSystem.CreateJumpEffect(
                            new Vector2(Position.X + Size.X / 2, p.Bounds.Top),
                            jumpColor
                        );
                        
                        landed = true;
                        jumpCount = 0; // Сбрасываем счетчик прыжков при приземлении
                        canDoubleJump = true;
                    }
                }
                
                if (!landed && Velocity.Y > 50)
                {
                    canDoubleJump = true; // Можно использовать двойной прыжок в воздухе
                }
            }

            // Игрок проиграл, если упал ниже камеры
            if (Position.Y > screen.CameraY + screen.ViewHeight + 200)
                screen.GameOver();
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            float rotation = animation.GetRotation(Velocity);
            Color tint = animation.GetTint();
            
            // Рисуем щит если есть
            if (HasShield)
            {
                Rectangle shieldRect = new Rectangle((int)Position.X - 5, (int)Position.Y - 5, (int)Size.X + 10, (int)Size.Y + 10);
                sb.Draw(pixel, shieldRect, new Color(0, 255, 255, 100));
            }
            
            // Рисуем эффект неуязвимости
            if (HasInvincibility)
            {
                Rectangle invincRect = new Rectangle((int)Position.X - 8, (int)Position.Y - 8, (int)Size.X + 16, (int)Size.Y + 16);
                float pulse = (float)(Math.Sin(powerUpTimer * 10) * 0.3 + 0.7);
                sb.Draw(pixel, invincRect, new Color(255, 0, 255, (int)(100 * pulse)));
            }
            
            Color drawColor = HasInvincibility ? Color.Lerp(tint, Color.Purple, 0.5f) : tint;
            sb.Draw(texture, Bounds, null, drawColor, rotation, Vector2.Zero, SpriteEffects.None, 0f);
        }
        
        public void ApplyPowerUp(PowerUp.PowerUpType type)
        {
            switch (type)
            {
                case PowerUp.PowerUpType.Jetpack:
                    HasJetpack = true;
                    break;
                case PowerUp.PowerUpType.Shield:
                    HasShield = true;
                    break;
                case PowerUp.PowerUpType.SlowMotion:
                    HasSlowMotion = true;
                    break;
                case PowerUp.PowerUpType.Invincibility:
                    HasInvincibility = true;
                    break;
                case PowerUp.PowerUpType.SpeedBoost:
                    HasSpeedBoost = true;
                    break;
                case PowerUp.PowerUpType.DoubleJump:
                    HasDoubleJump = true;
                    break;
            }
            powerUpTimer = powerUpDuration;
        }
        
        public void CollectCoin()
        {
            CoinScore += 10; // Каждая монета дает 10 очков
        }
        
        public int TotalScore => Score + CoinScore;
    }

    // ================= PLATFORM (BASE) =================
    public abstract class Platform : GameObject
    {
        public bool IsActive = true;
        protected Texture2D texture;

        public Platform(float x, float y)
        {
            Position = new Vector2(x, y);
            Size = new Vector2(80, 16);
        }

        public virtual void OnPlayerJump(Player player)
        {
            // Базовая логика прыжка
            player.Velocity.Y = -450f;
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            // Базовая логика обновления (переопределяется в наследниках)
        }
    }

    // ================= NORMAL PLATFORM =================
    public class NormalPlatform : Platform
    {
        public NormalPlatform(float x, float y) : base(x, y)
        {
            texture = Game1.NormalPlatformTexture;
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            if (IsActive)
                sb.Draw(texture, Bounds, Color.White);
        }
    }

    // ================= MOVING PLATFORM =================
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
                if (Position.X >= endX)
                {
                    Position.X = endX;
                    movingRight = false;
                }
            }
            else
            {
                Position.X -= speed * dt;
                if (Position.X <= startX)
                {
                    Position.X = startX;
                    movingRight = true;
                }
            }
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            if (IsActive)
                sb.Draw(texture, Bounds, Color.White);
        }
    }

    // ================= DISAPPEARING PLATFORM =================
    public class DisappearingPlatform : Platform
    {
        float disappearTimer = 0f;
        float disappearDelay = 0.5f; // Время до исчезновения после прыжка
        bool playerJumped = false;

        public DisappearingPlatform(float x, float y) : base(x, y)
        {
            texture = Game1.DisappearingPlatformTexture;
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            if (!IsActive) return;

            if (playerJumped)
            {
                disappearTimer += (float)gt.ElapsedGameTime.TotalSeconds;
                if (disappearTimer >= disappearDelay)
                {
                    IsActive = false;
                }
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
            if (IsActive)
            {
                // Мерцание перед исчезновением
                Color color = playerJumped && disappearTimer > disappearDelay * 0.5f
                    ? Color.Lerp(Color.White, Color.Transparent, (disappearTimer - disappearDelay * 0.5f) / (disappearDelay * 0.5f))
                    : Color.White;
                sb.Draw(texture, Bounds, color);
            }
        }
    }

    // ================= SPRING PLATFORM =================
    public class SpringPlatform : Platform
    {
        public SpringPlatform(float x, float y) : base(x, y)
        {
            texture = Game1.SpringPlatformTexture;
        }

        public override void OnPlayerJump(Player player)
        {
            // Пружинящая платформа дает больший прыжок
            player.Velocity.Y = -650f; // Выше обычного прыжка
        }

        float springAnimation = 0f;
        
        public override void Update(GameTime gt, PlayScreen screen)
        {
            base.Update(gt, screen);
            springAnimation += (float)gt.ElapsedGameTime.TotalSeconds * 5f;
        }
        
        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            if (IsActive)
            {
                // Рисуем основную платформу (красную)
                sb.Draw(texture, Bounds, Color.White);
                
                // Рисуем пружину (более заметную, желтую/красную)
                float springOffset = (float)(Math.Sin(springAnimation) * 2);
                Rectangle springRect = new Rectangle(
                    (int)Position.X + 15, 
                    (int)Position.Y + 2 + (int)springOffset, 
                    (int)Size.X - 30, 
                    12
                );
                
                // Градиент пружины
                Color springColor1 = Color.Yellow;
                Color springColor2 = Color.Orange;
                for (int i = 0; i < springRect.Height; i++)
                {
                    float t = (float)i / springRect.Height;
                    Color springColor = Color.Lerp(springColor1, springColor2, t);
                    Rectangle lineRect = new Rectangle(springRect.X, springRect.Y + i, springRect.Width, 1);
                    sb.Draw(pixel, lineRect, springColor);
                }
                
                // Волнистые линии пружины
                for (int x = springRect.X; x < springRect.X + springRect.Width; x += 8)
                {
                    int y1 = springRect.Y;
                    int y2 = springRect.Y + springRect.Height;
                    sb.Draw(pixel, new Rectangle(x, y1, 2, springRect.Height), Color.Orange);
                }
            }
        }
    }

    // ================= PLAY SCREEN =================
    public class PlayScreen
    {
        public Player Player;
        public List<Platform> Platforms = new List<Platform>();
        public List<Enemy> Enemies = new List<Enemy>();
        public List<Obstacle> Obstacles = new List<Obstacle>();
        public List<PowerUp> PowerUps = new List<PowerUp>();
        public List<Coin> Coins = new List<Coin>();
        public List<Boss> Bosses = new List<Boss>();
        ParallaxBackground background;
        public ParticleSystem particleSystem;
        public SpecialEvent specialEvent = new SpecialEvent();
        Random rnd = new Random();
        float lastBossSpawnHeight = 0f;
        float bossSpawnInterval = 2000f; // Босс каждые 2000 пикселей высоты

        public int ViewWidth => 480;
        public int ViewHeight => 800;

        // Камера следует за игроком вверх
        public float CameraY = 0f;
        float highestPlatformY = 0f; // Самый высокий Y (самое маленькое значение, так как Y растет вниз)

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
            CameraY = 0f;
            highestPlatformY = 0f; // Начинаем с 0 (самый верх)
            lastBossSpawnHeight = 0f;

            // Всегда добавляем обычную платформу прямо под игроком в начале игры
            float playerStartY = Player.Position.Y;
            float platformY = playerStartY + Player.Size.Y; // Платформа под игроком
            float platformX = Player.Position.X + (Player.Size.X / 2) - 40; // Центрируем платформу под игроком (платформа шириной 80)
            var startPlatform = new NormalPlatform(platformX, platformY);
            Platforms.Add(startPlatform);
            
            // Обновляем highestPlatformY для начальной платформы
            if (platformY < highestPlatformY)
                highestPlatformY = platformY;

            // Начальная генерация платформ (чаще внизу)
            GeneratePlatforms(760, 0, 20); // Генерируем 20 платформ внизу
        }

        // Генерация платформ с учетом высоты (чаще внизу, реже вверху)
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
                
                // Обновляем самую высокую платформу (меньшее значение Y = выше)
                if (y < highestPlatformY)
                    highestPlatformY = y;
                    
                y -= step;
            }
        }

        Platform CreateRandomPlatform(float x, float y)
        {
            int platformType = rnd.Next(0, 100);

            if (platformType < 50) // 50% - обычные платформы
            {
                return new NormalPlatform(x, y);
            }
            else if (platformType < 70) // 20% - движущиеся платформы
            {
                float range = rnd.Next(60, 120);
                return new MovingPlatform(x, y, range, rnd.Next(50, 100));
            }
            else if (platformType < 85) // 15% - исчезающие платформы
            {
                return new DisappearingPlatform(x, y);
            }
            else // 15% - пружинящие платформы
            {
                return new SpringPlatform(x, y);
            }
        }

        public void Update(GameTime gt)
        {
            // Обновляем камеру - следует за игроком вверх
            float targetCameraY = Player.Position.Y - ViewHeight * 0.3f; // Игрок в верхней трети экрана
            if (targetCameraY < CameraY)
                CameraY = targetCameraY;

            // Удаляем платформы, которые ушли далеко вниз
            Platforms.RemoveAll(p => p.Position.Y > CameraY + ViewHeight + 200);

            // Генерируем новые платформы выше
            float currentTop = highestPlatformY;
            float playerY = Player.Position.Y;
            float targetTop = playerY - ViewHeight * 1.5f; // Генерируем платформы достаточно высоко над игроком

            // Вычисляем частоту генерации в зависимости от высоты
            // Чем выше, тем реже платформы
            float heightFactor = Math.Max(0, (-playerY) / 5000f); // Нормализуем высоту (playerY отрицательный вверх)
            float minDistance = 40f + heightFactor * 80f; // От 40 до 120 пикселей между платформами
            float maxDistance = 60f + heightFactor * 100f; // От 60 до 160 пикселей

            // Генерируем платформы выше текущей позиции игрока (только если нужно)
            if (currentTop > targetTop)
            {
                while (currentTop > targetTop)
            {
                // Внизу генерируем чаще (меньше расстояние)
                float distance;
                if (currentTop > 500) // Внизу (большие значения Y)
                {
                    distance = rnd.Next(35, 55); // Чаще внизу
                }
                else // Выше
                {
                    distance = rnd.Next((int)minDistance, (int)maxDistance);
                }
                
                currentTop -= distance;

                Platform platform = CreateRandomPlatform(rnd.Next(40, 360), currentTop);
                Platforms.Add(platform);
                
                // Обновляем самую высокую платформу (меньшее значение Y = выше)
                if (currentTop < highestPlatformY)
                    highestPlatformY = currentTop;
                }
            }

            // Обновляем параллакс-фон
            background.Update(CameraY);

            // Удаляем объекты, которые ушли далеко вниз
            Enemies.RemoveAll(e => e.Position.Y > CameraY + ViewHeight + 200);
            Obstacles.RemoveAll(o => o.Position.Y > CameraY + ViewHeight + 200);
            PowerUps.RemoveAll(p => p.Position.Y > CameraY + ViewHeight + 200);
            Coins.RemoveAll(c => c.Position.Y > CameraY + ViewHeight + 200);
            Bosses.RemoveAll(b => b.Health <= 0 || b.Position.Y > CameraY + ViewHeight + 200);
            
            // Генерируем боссов на определенных высотах
            float currentHeight = -Player.Position.Y; // Высота игрока (отрицательная Y = выше)
            if (currentHeight - lastBossSpawnHeight >= bossSpawnInterval)
            {
                lastBossSpawnHeight = currentHeight;
                float spawnY = Player.Position.Y - ViewHeight * 0.5f;
                Bosses.Add(new Boss(rnd.Next(60, 360), spawnY, rnd.Next(40, 70), rnd.Next(100, 200)));
            }

            // Генерируем врагов, препятствия, бонусы и монеты
            if (rnd.Next(0, 100) < 4) // 4% шанс на каждом кадре
            {
                float spawnY = playerY - ViewHeight * 0.8f;
                if (spawnY < highestPlatformY)
                {
                    int type = rnd.Next(0, 100);
                    if (type < 15) // 15% - обычный враг
                    {
                        Enemies.Add(new Enemy(rnd.Next(40, 360), spawnY, rnd.Next(30, 60), rnd.Next(50, 100)));
                    }
                    else if (type < 25) // 10% - летающий враг
                    {
                        Enemies.Add(new FlyingEnemy(rnd.Next(40, 360), spawnY, rnd.Next(30, 60), rnd.Next(50, 100), rnd.Next(20, 40), rnd.Next(30, 60)));
                    }
                    else if (type < 35) // 10% - стреляющий враг
                    {
                        Enemies.Add(new ShootingEnemy(rnd.Next(40, 360), spawnY, rnd.Next(30, 60), rnd.Next(50, 100)));
                    }
                    else if (type < 40) // 5% - преследующий враг
                    {
                        var chasingEnemy = new ChasingEnemy(rnd.Next(40, 360), spawnY, rnd.Next(30, 60), rnd.Next(50, 100));
                        chasingEnemy.SetTarget(Player);
                        Enemies.Add(chasingEnemy);
                    }
                    else if (type < 50) // 10% - черная дыра (увеличено для тестирования)
                    {
                        Enemies.Add(new BlackHole(rnd.Next(40, 360), spawnY));
                    }
                    else if (type < 60) // 10% - препятствие
                    {
                        Obstacles.Add(new Obstacle(rnd.Next(40, 360), spawnY));
                    }
                    else if (type < 70) // 15% - бонус
                    {
                        PowerUp.PowerUpType powerType = (PowerUp.PowerUpType)rnd.Next(0, 6);
                        PowerUps.Add(new PowerUp(rnd.Next(40, 360), spawnY, powerType));
                    }
                    else if (type < 85) // 15% - монета
                    {
                        Coins.Add(new Coin(rnd.Next(40, 360), spawnY));
                    }
                }
            }

            // Обновляем все объекты
            foreach (var platform in Platforms)
                platform.Update(gt, this);
            foreach (var enemy in Enemies)
            {
                enemy.Update(gt, this);
                // Обновляем пули стреляющих врагов (пули уже обновляются в ShootingEnemy.Update)
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

            // Проверяем коллизии
            // Враги
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

            // Препятствия
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

            // Бонусы
            foreach (var powerUp in PowerUps.ToList())
            {
                if (powerUp.CheckCollision(Player))
                {
                    Player.ApplyPowerUp(powerUp.Type);
                    
                    // Эффект частиц при сборе бонуса
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

            // Монеты
            foreach (var coin in Coins.ToList())
            {
                if (coin.CheckCollision(Player))
                {
                    Player.CollectCoin();
                    
                    // Эффект частиц при сборе монеты
                    particleSystem.CreateCollectEffect(
                        new Vector2(coin.Position.X + coin.Size.X / 2, coin.Position.Y + coin.Size.Y / 2),
                        Color.Gold
                    );
                    
                    Coins.Remove(coin);
                }
            }

            // Боссы
            foreach (var boss in Bosses.ToList())
            {
                // Проверяем столкновение с игроком
                if (boss.CheckCollision(Player))
                {
                    if (!Player.HasInvincibility && !Player.HasShield)
                    {
                        // Игрок получает урон от босса
                        GameOver();
                    }
                }

                // Проверяем столкновение пуль босса с игроком
                foreach (var bullet in boss.Bullets.ToList())
                {
                    if (bullet.CheckCollision(Player))
                    {
                        if (!Player.HasInvincibility && !Player.HasShield)
                            GameOver();
                        boss.Bullets.Remove(bullet);
                    }
                }

                // Игрок может атаковать босса (прыгая на него сверху)
                if (Player.Velocity.Y > 0 && 
                    Player.Bounds.Bottom >= boss.Bounds.Top &&
                    Player.Bounds.Bottom <= boss.Bounds.Top + 20 &&
                    Player.Bounds.Right > boss.Bounds.Left &&
                    Player.Bounds.Left < boss.Bounds.Right)
                {
                    // Игрок прыгнул на босса сверху
                    if (boss.TakeDamage())
                    {
                        // Босс побежден - эффект взрыва
                        particleSystem.CreateComboEffect(
                            new Vector2(boss.Position.X + boss.Size.X / 2, boss.Position.Y + boss.Size.Y / 2),
                            20
                        );
                        Player.Combo.OnJump(Player.gameTime); // Бонус за победу над боссом
                        Bosses.Remove(boss);
                    }
                    else
                    {
                        // Босс получил урон, но не побежден - отталкиваем игрока
                        Player.Velocity.Y = -400f;
                    }
                }
            }
            
            // Обновляем частицы
            particleSystem.Update(gt);
            
            // Обновляем специальные события
            specialEvent.Update(gt, this);
            
            // Генерируем специальные события случайным образом
            if (rnd.Next(0, 10000) < 2) // 0.02% шанс на каждом кадре
            {
                if (rnd.Next(0, 2) == 0)
                    specialEvent.Start(SpecialEventType.MeteorShower, 10f); // 10 секунд метеоритного дождя
                else
                    specialEvent.Start(SpecialEventType.Wind, 8f); // 8 секунд ветра
            }
        }

        public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFont font)
        {
            // Рисуем параллакс-фон (без трансформации камеры)
            sb.Begin();
            background.Draw(sb);
            sb.End();

            // Применяем смещение камеры для игровых объектов
            Matrix transform = Matrix.CreateTranslation(0, -CameraY, 0);
            sb.Begin(transformMatrix: transform);

            // Рисуем платформы в мировых координатах
            foreach (var p in Platforms)
            {
                if (p.Position.Y >= CameraY - 100 && p.Position.Y <= CameraY + ViewHeight + 100)
                    p.Draw(sb, pixel);
            }

            // Рисуем врагов
            foreach (var enemy in Enemies)
            {
                if (enemy.Position.Y >= CameraY - 100 && enemy.Position.Y <= CameraY + ViewHeight + 100)
                    enemy.Draw(sb, pixel);
            }

            // Рисуем препятствия
            foreach (var obstacle in Obstacles)
            {
                if (obstacle.Position.Y >= CameraY - 100 && obstacle.Position.Y <= CameraY + ViewHeight + 100)
                    obstacle.Draw(sb, pixel);
            }

            // Рисуем бонусы
            foreach (var powerUp in PowerUps)
            {
                if (powerUp.Position.Y >= CameraY - 100 && powerUp.Position.Y <= CameraY + ViewHeight + 100)
                    powerUp.Draw(sb, pixel);
            }

            // Рисуем монеты
            foreach (var coin in Coins)
            {
                if (coin.Position.Y >= CameraY - 100 && coin.Position.Y <= CameraY + ViewHeight + 100)
                    coin.Draw(sb, pixel);
            }

            // Рисуем пули стреляющих врагов
            foreach (var enemy in Enemies)
            {
                if (enemy is ShootingEnemy shootingEnemy)
                {
                    foreach (var bullet in shootingEnemy.Bullets)
                    {
                        if (bullet.Position.Y >= CameraY - 100 && bullet.Position.Y <= CameraY + ViewHeight + 100)
                            bullet.Draw(sb, pixel);
                    }
                }
            }

            // Рисуем игрока
            Player.Draw(sb, pixel);
            
            // Рисуем частицы
            particleSystem.Draw(sb, CameraY);

            sb.End();

            // Рисуем UI (счет и бонусы) без трансформации камеры
            sb.Begin();
            
            DrawScoreUI(sb, pixel, font);
            DrawPowerUpIndicators(sb, pixel, font);
            DrawComboUI(sb, pixel, font);
            DrawSpecialEventUI(sb, pixel, font);
            
            sb.End();
        }

        public bool IsGameOver = false;
        
        float scorePulse = 0f;
        
        void DrawScoreUI(SpriteBatch sb, Texture2D pixel, SpriteFont font)
        {
            scorePulse += 0.05f;
            if (scorePulse > MathHelper.TwoPi) scorePulse -= MathHelper.TwoPi;
            
            int score = Player.TotalScore;
            string scoreText = $"Score: {score:N0}";
            Vector2 scoreSize = font.MeasureString(scoreText);
            Vector2 scorePos = new Vector2(15, 15);
            
            // Градиентный фон с закругленными углами (имитация)
            int padding = 12;
            Rectangle scoreBg = new Rectangle((int)scorePos.X - padding, (int)scorePos.Y - padding, 
                (int)scoreSize.X + padding * 2, (int)scoreSize.Y + padding * 2);
            
            // Тень
            Rectangle shadowRect = new Rectangle(scoreBg.X + 3, scoreBg.Y + 3, scoreBg.Width, scoreBg.Height);
            sb.Draw(pixel, shadowRect, new Color(0, 0, 0, 100));
            
            // Градиентный фон (темно-синий к фиолетовому)
            Color bgColor1 = new Color(20, 30, 60, 220);
            Color bgColor2 = new Color(40, 20, 60, 220);
            for (int i = 0; i < scoreBg.Height; i++)
            {
                float t = (float)i / scoreBg.Height;
                Color gradientColor = Color.Lerp(bgColor1, bgColor2, t);
                Rectangle lineRect = new Rectangle(scoreBg.X, scoreBg.Y + i, scoreBg.Width, 1);
                sb.Draw(pixel, lineRect, gradientColor);
            }
            
            // Рамка с эффектом свечения
            float glow = (float)(Math.Sin(scorePulse) * 0.3 + 0.7);
            Color borderColor = Color.Lerp(new Color(100, 150, 255), new Color(150, 100, 255), glow);
            sb.Draw(pixel, new Rectangle(scoreBg.X, scoreBg.Y, scoreBg.Width, 2), borderColor); // Верх
            sb.Draw(pixel, new Rectangle(scoreBg.X, scoreBg.Y + scoreBg.Height - 2, scoreBg.Width, 2), borderColor); // Низ
            sb.Draw(pixel, new Rectangle(scoreBg.X, scoreBg.Y, 2, scoreBg.Height), borderColor); // Лево
            sb.Draw(pixel, new Rectangle(scoreBg.X + scoreBg.Width - 2, scoreBg.Y, 2, scoreBg.Height), borderColor); // Право
            
            // Текст счета с эффектом свечения
            Color textColor = Color.Lerp(Color.White, new Color(200, 220, 255), (float)Math.Sin(scorePulse) * 0.3f);
            sb.DrawString(font, scoreText, scorePos, textColor);
        }
        
        void DrawPowerUpIndicators(SpriteBatch sb, Texture2D pixel, SpriteFont font)
        {
            float yPos = 70;
            float xPos = 15;
            float spacing = 35;
            int index = 0;
            
            if (Player.HasJetpack)
            {
                DrawPowerUpIcon(sb, pixel, font, xPos, yPos + index * spacing, "JET", Color.Blue, Player.powerUpTimer, Player.powerUpDuration);
                index++;
            }
            if (Player.HasShield)
            {
                DrawPowerUpIcon(sb, pixel, font, xPos, yPos + index * spacing, "SHIELD", Color.Cyan, Player.powerUpTimer, Player.powerUpDuration);
                index++;
            }
            if (Player.HasSlowMotion)
            {
                DrawPowerUpIcon(sb, pixel, font, xPos, yPos + index * spacing, "SLOW", Color.Yellow, Player.powerUpTimer, Player.powerUpDuration);
                index++;
            }
            if (Player.HasInvincibility)
            {
                DrawPowerUpIcon(sb, pixel, font, xPos, yPos + index * spacing, "INV", Color.Purple, Player.powerUpTimer, Player.powerUpDuration);
                index++;
            }
            if (Player.HasSpeedBoost)
            {
                DrawPowerUpIcon(sb, pixel, font, xPos, yPos + index * spacing, "SPEED", Color.Orange, Player.powerUpTimer, Player.powerUpDuration);
                index++;
            }
            if (Player.HasDoubleJump)
            {
                DrawPowerUpIcon(sb, pixel, font, xPos, yPos + index * spacing, "2X JUMP", Color.Green, Player.powerUpTimer, Player.powerUpDuration);
                index++;
            }
        }
        
        void DrawPowerUpIcon(SpriteBatch sb, Texture2D pixel, SpriteFont font, float x, float y, string text, Color color, float timer, float duration)
        {
            float progress = timer / duration;
            int iconSize = 28;
            
            // Фон иконки
            Rectangle iconBg = new Rectangle((int)x, (int)y, iconSize, iconSize);
            sb.Draw(pixel, iconBg, new Color(0, 0, 0, 180));
            
            // Иконка с пульсацией
            float pulse = (float)(Math.Sin(progress * 20) * 0.3 + 0.7);
            Color iconColor = Color.Lerp(color, Color.White, pulse * 0.5f);
            Rectangle iconRect = new Rectangle((int)x + 2, (int)y + 2, iconSize - 4, iconSize - 4);
            sb.Draw(pixel, iconRect, iconColor);
            
            // Полоса прогресса
            int barWidth = iconSize - 4;
            int barHeight = 3;
            Rectangle progressBg = new Rectangle((int)x + 2, (int)(y + iconSize - 5), barWidth, barHeight);
            sb.Draw(pixel, progressBg, new Color(50, 50, 50, 200));
            
            Rectangle progressBar = new Rectangle((int)x + 2, (int)(y + iconSize - 5), (int)(barWidth * progress), barHeight);
            sb.Draw(pixel, progressBar, color);
            
            // Текст рядом с иконкой
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
                
                // Фон для комбо
                Rectangle comboBg = new Rectangle((int)comboPos.X - 10, (int)comboPos.Y - 5,
                    (int)comboSize.X + 20, (int)comboSize.Y + 10);
                
                // Градиентный фон
                Color bg1 = new Color(255, 200, 0, 200);
                Color bg2 = new Color(255, 100, 0, 200);
                for (int i = 0; i < comboBg.Height; i++)
                {
                    float t = (float)i / comboBg.Height;
                    Color gradColor = Color.Lerp(bg1, bg2, t);
                    sb.Draw(pixel, new Rectangle(comboBg.X, comboBg.Y + i, comboBg.Width, 1), gradColor);
                }
                
                // Текст комбо
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
                    
                    // Пульсирующий текст
                    float pulse = (float)(Math.Sin(Player.gameTime * 5) * 0.3 + 0.7);
                    Color eventColor = specialEvent.Type == SpecialEventType.MeteorShower ? 
                        Color.Lerp(Color.Orange, Color.Red, pulse) :
                        Color.Lerp(Color.Cyan, Color.Blue, pulse);
                    
                    // Тень
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

    // ================= SCREEN MANAGER =================
    public class ScreenManager
    {
        Texture2D pixel;
        SpriteFont font;
        HighScoreManager highScore;
        PlayScreen play;
        MenuScreen menu;
        GameOverModal gameOverModal;
        GameState currentState = GameState.MainMenu;
        KeyboardState previousKeyboardState;

        public ScreenManager(Game1 g, Texture2D p, SpriteFont f, HighScoreManager h)
        {
            pixel = p;
            font = f;
            highScore = h;
            menu = new MenuScreen(f, p);
            gameOverModal = new GameOverModal(f, p);
        }

        public void Setup()
        {
            play = new PlayScreen();
            previousKeyboardState = Keyboard.GetState();
        }

        public void Update(GameTime gt)
        {
            KeyboardState currentKeyboardState = Keyboard.GetState();

            if (currentState == GameState.Playing)
            {
                // Не обновляем игру, если модальное окно Game Over видимо
                if (!gameOverModal.IsVisible)
                {
                    if (currentKeyboardState.IsKeyDown(Keys.Escape) && previousKeyboardState.IsKeyUp(Keys.Escape))
                    {
                        currentState = GameState.Paused;
                        menu.CurrentState = GameState.Paused;
                    }
                    else
                    {
                        play.Update(gt);
                        
                        // Проверяем GameOver
                        if (play.IsGameOver || play.Player.Position.Y > play.CameraY + play.ViewHeight + 200)
                        {
                            gameOverModal.Show(play.Player.Score);
                            play.IsGameOver = false;
                        }
                    }
                }
            }
            else if (currentState == GameState.MainMenu || currentState == GameState.Paused || currentState == GameState.Settings)
            {
                menu.Update(gt, currentKeyboardState, previousKeyboardState);
                GameState newState = menu.CurrentState;
                
                // Если переходим из паузы в игру - просто продолжаем (не создаем новый PlayScreen)
                if (currentState == GameState.Paused && newState == GameState.Playing)
                {
                    currentState = GameState.Playing;
                }
                // Если переходим из главного меню в игру - создаем новый PlayScreen
                else if (currentState == GameState.MainMenu && newState == GameState.Playing)
                {
                    play = new PlayScreen();
                    currentState = GameState.Playing;
                }
                // Для остальных переходов просто обновляем состояние
                else
                {
                    currentState = newState;
                }
            }

            // Обработка рестарта при Game Over (проверяем в любом состоянии, если модальное окно видимо)
            if (gameOverModal.IsVisible)
            {
                if (currentKeyboardState.IsKeyDown(Keys.Enter) && previousKeyboardState.IsKeyUp(Keys.Enter))
                {
                    gameOverModal.Hide();
                    play = new PlayScreen();
                    currentState = GameState.Playing;
                    menu.CurrentState = GameState.Playing;
                }
            }

            previousKeyboardState = currentKeyboardState;
        }

        public void Draw(SpriteBatch sb)
        {
            if (currentState == GameState.Playing)
            {
                play.Draw(sb, pixel, font);
                if (gameOverModal.IsVisible)
                {
                    gameOverModal.Draw(sb, play.ViewWidth, play.ViewHeight);
                }
            }
            else
            {
                menu.Draw(sb, 480, 800);
            }
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

        public HighScoreManager(string path)
        {
            file = path;
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
                if (dist < 14)
                    data[i] = Color.Red;
                else if (dist < 15)
                    data[i] = Color.DarkRed;
                else
                    data[i] = Color.Transparent;
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
                if (Position.X >= startX + moveRange)
                {
                    Position.X = startX + moveRange;
                    movingRight = false;
                }
            }
            else
            {
                Position.X -= moveSpeed * dt;
                if (Position.X <= startX)
                {
                    Position.X = startX;
                    movingRight = true;
                }
            }
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            sb.Draw(texture, Bounds, Color.White);
        }

        public bool CheckCollision(Player player)
        {
            return Bounds.Intersects(player.Bounds);
        }
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
                if ((x < 5 || x >= 35 || y < 5 || y >= 35))
                    data[i] = Color.DarkGray;
                else
                    data[i] = Color.Gray;
            }
            tex.SetData(data);
            return tex;
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            sb.Draw(texture, Bounds, Color.White);
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
                         Color.Green; // DoubleJump
            
            for (int i = 0; i < data.Length; i++)
            {
                int x = i % 32;
                int y = i / 32;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
                if (dist < 12)
                    data[i] = color;
                else if (dist < 14)
                    data[i] = Color.Lerp(color, Color.White, 0.5f);
                else
                    data[i] = Color.Transparent;
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

            // Определяем состояние анимации
            if (Math.Abs(velocity.X) > 10)
                CurrentState = AnimationState.Moving;
            else if (velocity.Y < -50)
                CurrentState = AnimationState.Jumping;
            else if (velocity.Y > 50)
                CurrentState = AnimationState.Falling;
            else
                CurrentState = AnimationState.Idle;

            // Обновляем кадр анимации
            if (animationTimer >= frameTime)
            {
                animationTimer = 0f;
                currentFrame = (currentFrame + 1) % 4; // 4 кадра анимации
            }
        }

        public float GetRotation(Vector2 velocity)
        {
            // Наклон персонажа при движении
            if (Math.Abs(velocity.X) > 10)
                return velocity.X > 0 ? 0.1f : -0.1f;
            return 0f;
        }

        public Color GetTint()
        {
            // Легкое изменение цвета для эффекта
            return Color.White;
        }
    }

    // ================= MENU SYSTEM =================
    public enum GameState { MainMenu, Playing, Paused, GameOver, Settings }
    
    public class MenuScreen
    {
        SpriteFont font;
        Texture2D pixel;
        GameState currentState;
        int selectedOption = 0;
        List<string> mainMenuOptions = new List<string> { "Start Game", "Settings", "Exit" };
        List<string> pauseMenuOptions = new List<string> { "Resume", "Main Menu", "Exit" };
        List<string> settingsOptions = new List<string> { "Back" };

        public MenuScreen(SpriteFont f, Texture2D p)
        {
            font = f;
            pixel = p;
            currentState = GameState.MainMenu;
        }

        public void Update(GameTime gt, KeyboardState currentKeys, KeyboardState previousKeys)
        {
            if (currentState == GameState.MainMenu)
            {
                if (currentKeys.IsKeyDown(Keys.Up) && previousKeys.IsKeyUp(Keys.Up))
                {
                    selectedOption = (selectedOption - 1 + mainMenuOptions.Count) % mainMenuOptions.Count;
                }
                if (currentKeys.IsKeyDown(Keys.Down) && previousKeys.IsKeyUp(Keys.Down))
                {
                    selectedOption = (selectedOption + 1) % mainMenuOptions.Count;
                }
                if (currentKeys.IsKeyDown(Keys.Enter) && previousKeys.IsKeyUp(Keys.Enter))
                {
                    HandleMainMenuSelection();
                }
            }
            else if (currentState == GameState.Paused)
            {
                if (currentKeys.IsKeyDown(Keys.Up) && previousKeys.IsKeyUp(Keys.Up))
                {
                    selectedOption = (selectedOption - 1 + pauseMenuOptions.Count) % pauseMenuOptions.Count;
                }
                if (currentKeys.IsKeyDown(Keys.Down) && previousKeys.IsKeyUp(Keys.Down))
                {
                    selectedOption = (selectedOption + 1) % pauseMenuOptions.Count;
                }
                if (currentKeys.IsKeyDown(Keys.Enter) && previousKeys.IsKeyUp(Keys.Enter))
                {
                    HandlePauseMenuSelection();
                }
            }
        }

        void HandleMainMenuSelection()
        {
            switch (selectedOption)
            {
                case 0: // Start Game
                    currentState = GameState.Playing;
                    break;
                case 1: // Settings
                    currentState = GameState.Settings;
                    break;
                case 2: // Exit
                    Game1.Instance.Exit();
                    break;
            }
        }

        void HandlePauseMenuSelection()
        {
            switch (selectedOption)
            {
                case 0: // Resume
                    currentState = GameState.Playing;
                    break;
                case 1: // Main Menu
                    currentState = GameState.MainMenu;
                    break;
                case 2: // Exit
                    Game1.Instance.Exit();
                    break;
            }
        }

        public void Draw(SpriteBatch sb, int viewWidth, int viewHeight)
        {
            sb.Begin();
            
            if (currentState == GameState.MainMenu)
            {
                DrawMenu(sb, viewWidth, viewHeight, "Doodle Jump", mainMenuOptions);
            }
            else if (currentState == GameState.Paused)
            {
                // Полупрозрачный фон
                sb.Draw(pixel, new Rectangle(0, 0, viewWidth, viewHeight), new Color(0, 0, 0, 150));
                DrawMenu(sb, viewWidth, viewHeight, "Paused", pauseMenuOptions);
            }
            else if (currentState == GameState.Settings)
            {
                DrawMenu(sb, viewWidth, viewHeight, "Settings", settingsOptions);
            }
            
            sb.End();
        }

        float menuPulse = 0f;
        
        void DrawMenu(SpriteBatch sb, int viewWidth, int viewHeight, string title, List<string> options)
        {
            menuPulse += 0.03f;
            if (menuPulse > MathHelper.TwoPi) menuPulse -= MathHelper.TwoPi;
            
            // Фон меню с градиентом
            for (int y = 0; y < viewHeight; y++)
            {
                float t = (float)y / viewHeight;
                Color bgColor = Color.Lerp(new Color(15, 15, 35), new Color(25, 20, 40), t);
                sb.Draw(pixel, new Rectangle(0, y, viewWidth, 1), bgColor);
            }
            
            // Заголовок с эффектом свечения
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2((viewWidth - titleSize.X) / 2, 80);
            
            // Тень заголовка
            sb.DrawString(font, title, titlePos + new Vector2(3, 3), new Color(0, 0, 0, 150));
            
            // Заголовок с пульсацией
            float titleGlow = (float)(Math.Sin(menuPulse) * 0.3 + 0.7);
            Color titleColor = Color.Lerp(new Color(255, 200, 100), new Color(255, 255, 150), titleGlow);
            sb.DrawString(font, title, titlePos, titleColor);

            // Опции меню
            for (int i = 0; i < options.Count; i++)
            {
                Vector2 optionSize = font.MeasureString(options[i]);
                Vector2 optionPos = new Vector2((viewWidth - optionSize.X) / 2, 250 + i * 70);
                
                bool isSelected = i == selectedOption;
                
                if (isSelected)
                {
                    // Фон для выбранной опции
                    int padding = 15;
                    Rectangle optionBg = new Rectangle((int)optionPos.X - padding, (int)optionPos.Y - 5, 
                        (int)optionSize.X + padding * 2, (int)optionSize.Y + 10);
                    
                    // Градиентный фон
                    Color bg1 = new Color(100, 150, 255, 150);
                    Color bg2 = new Color(150, 100, 255, 150);
                    for (int j = 0; j < optionBg.Height; j++)
                    {
                        float t = (float)j / optionBg.Height;
                        Color gradColor = Color.Lerp(bg1, bg2, t);
                        sb.Draw(pixel, new Rectangle(optionBg.X, optionBg.Y + j, optionBg.Width, 1), gradColor);
                    }
                    
                    // Рамка
                    float borderGlow = (float)(Math.Sin(menuPulse * 2) * 0.5 + 0.5);
                    Color borderColor = Color.Lerp(new Color(150, 200, 255), new Color(255, 200, 255), borderGlow);
                    sb.Draw(pixel, new Rectangle(optionBg.X, optionBg.Y, optionBg.Width, 2), borderColor);
                    sb.Draw(pixel, new Rectangle(optionBg.X, optionBg.Y + optionBg.Height - 2, optionBg.Width, 2), borderColor);
                    sb.Draw(pixel, new Rectangle(optionBg.X, optionBg.Y, 2, optionBg.Height), borderColor);
                    sb.Draw(pixel, new Rectangle(optionBg.X + optionBg.Width - 2, optionBg.Y, 2, optionBg.Height), borderColor);
                    
                    // Стрелка выбора
                    float arrowOffset = (float)(Math.Sin(menuPulse * 3) * 5);
                    sb.DrawString(font, ">", new Vector2(optionPos.X - 30 + arrowOffset, optionPos.Y), Color.Yellow);
                    sb.DrawString(font, "<", new Vector2(optionPos.X + optionSize.X + 10 - arrowOffset, optionPos.Y), Color.Yellow);
                }
                
                Color textColor = isSelected ? Color.White : new Color(200, 200, 200);
                sb.DrawString(font, options[i], optionPos, textColor);
            }
        }

        public GameState CurrentState
        {
            get => currentState;
            set => currentState = value;
        }
    }

    // ================= GAME OVER MODAL =================
    public class GameOverModal
    {
        SpriteFont font;
        Texture2D pixel;
        bool isVisible = false;
        int finalScore = 0;

        public GameOverModal(SpriteFont f, Texture2D p)
        {
            font = f;
            pixel = p;
        }

        public void Show(int score)
        {
            isVisible = true;
            finalScore = score;
        }

        public void Hide()
        {
            isVisible = false;
        }

        public bool IsVisible => isVisible;

        public void Update(KeyboardState currentKeys, KeyboardState previousKeys)
        {
            // Обработка Enter перенесена в ScreenManager для рестарта игры
        }

        public void Draw(SpriteBatch sb, int viewWidth, int viewHeight)
        {
            if (!isVisible) return;

            sb.Begin();
            
            // Полупрозрачный фон
            sb.Draw(pixel, new Rectangle(0, 0, viewWidth, viewHeight), new Color(0, 0, 0, 200));

            // Модальное окно
            int modalWidth = 300;
            int modalHeight = 200;
            int modalX = (viewWidth - modalWidth) / 2;
            int modalY = (viewHeight - modalHeight) / 2;

            Rectangle modalRect = new Rectangle(modalX, modalY, modalWidth, modalHeight);
            sb.Draw(pixel, modalRect, Color.DarkGray);
            sb.Draw(pixel, new Rectangle(modalX + 2, modalY + 2, modalWidth - 4, modalHeight - 4), Color.Gray);

            // Текст
            string gameOverText = "Game Over";
            string scoreText = $"Score: {finalScore}";
            string restartText = "Press Enter to Restart";

            Vector2 gameOverSize = font.MeasureString(gameOverText);
            Vector2 scoreSize = font.MeasureString(scoreText);
            Vector2 restartSize = font.MeasureString(restartText);

            sb.DrawString(font, gameOverText, new Vector2(modalX + (modalWidth - gameOverSize.X) / 2, modalY + 30), Color.Red);
            sb.DrawString(font, scoreText, new Vector2(modalX + (modalWidth - scoreSize.X) / 2, modalY + 80), Color.White);
            sb.DrawString(font, restartText, new Vector2(modalX + (modalWidth - restartSize.X) / 2, modalY + 130), Color.Yellow);
            
            sb.End();
        }
    }

    // ================= PARALLAX BACKGROUND =================
    public class ParallaxBackground
    {
        List<ParallaxLayer> layers = new List<ParallaxLayer>();
        float cameraY = 0f;

        public ParallaxBackground(GraphicsDevice device)
        {
            // Слой 1: Звезды (самый дальний, движется медленнее всего)
            layers.Add(new ParallaxLayer(device, 480, 2000, Color.White, 0.1f, 50));
            
            // Слой 2: Планеты (средний план)
            layers.Add(new ParallaxLayer(device, 480, 2000, Color.LightBlue, 0.2f, 20));
            
            // Слой 3: Облака (ближний план, движется быстрее)
            layers.Add(new ParallaxLayer(device, 480, 2000, Color.LightGray, 0.4f, 15));
        }

        public void Update(float cameraY)
        {
            this.cameraY = cameraY;
            foreach (var layer in layers)
            {
                layer.Update(cameraY);
            }
        }

        public void Draw(SpriteBatch sb)
        {
            foreach (var layer in layers)
            {
                layer.Draw(sb, cameraY);
            }
        }
    }

    public class ParallaxLayer
    {
        Texture2D texture;
        List<Vector2> elements = new List<Vector2>();
        float parallaxSpeed;
        Color color;
        int elementSize;
        Random rnd = new Random();
        int viewHeight;

        public ParallaxLayer(GraphicsDevice device, int width, int height, Color elementColor, float speed, int size)
        {
            texture = CreateElementTexture(device, size, elementColor);
            parallaxSpeed = speed;
            color = elementColor;
            elementSize = size;
            viewHeight = 800;

            // Генерируем элементы фона
            int count = (int)(height / (size * 2));
            for (int i = 0; i < count; i++)
            {
                elements.Add(new Vector2(rnd.Next(0, width), rnd.Next(0, height)));
            }
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
                
                if (dist < size / 2 - 2)
                    data[i] = col;
                else if (dist < size / 2)
                    data[i] = Color.Lerp(col, Color.Transparent, 0.5f);
                else
                    data[i] = Color.Transparent;
            }
            
            tex.SetData(data);
            return tex;
        }

        public void Update(float cameraY)
        {
            // Обновляем позиции элементов с учетом параллакса
            for (int i = 0; i < elements.Count; i++)
            {
                var elem = elements[i];
                float worldY = elem.Y - cameraY * parallaxSpeed;
                
                // Если элемент ушел за экран, перемещаем его выше
                if (worldY > viewHeight + 100)
                {
                    elements[i] = new Vector2(elem.X, cameraY - viewHeight - 100);
                }
                else if (worldY < -100)
                {
                    elements[i] = new Vector2(elem.X, cameraY + viewHeight + 100);
                }
            }
        }

        public void Draw(SpriteBatch sb, float cameraY)
        {
            foreach (var elem in elements)
            {
                // Вычисляем экранную позицию с учетом параллакса
                float screenY = elem.Y - cameraY * parallaxSpeed;
                
                // Отрисовываем элемент, если он виден на экране
                if (screenY >= -elementSize && screenY <= viewHeight + elementSize)
                {
                    sb.Draw(texture, new Vector2(elem.X, screenY), color);
                }
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
                if (Position.Y <= startY - verticalRange)
                {
                    Position.Y = startY - verticalRange;
                    movingUp = false;
                }
            }
            else
            {
                Position.Y += verticalSpeed * dt;
                if (Position.Y >= startY + verticalRange)
                {
                    Position.Y = startY + verticalRange;
                    movingUp = true;
                }
            }
        }
    }

    // ================= SHOOTING ENEMY =================
    public class ShootingEnemy : Enemy
    {
        public List<Bullet> Bullets = new List<Bullet>();
        float shootTimer = 0f;
        float shootInterval = 2f;

        public ShootingEnemy(float x, float y, float speed, float range) : base(x, y, speed, range)
        {
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            base.Update(gt, screen);
            
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;
            shootTimer += dt;
            
            if (shootTimer >= shootInterval)
            {
                shootTimer = 0f;
                // Стреляем вниз
                Bullets.Add(new Bullet(Position.X + Size.X / 2, Position.Y + Size.Y, 200f, true));
            }
            
            // Обновляем пули
            foreach (var bullet in Bullets.ToList())
            {
                bullet.Update(gt, screen);
                if (bullet.Position.Y > screen.CameraY + screen.ViewHeight + 200)
                {
                    Bullets.Remove(bullet);
                }
            }
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            base.Draw(sb, pixel);
            
            // Рисуем пули
            foreach (var bullet in Bullets)
            {
                bullet.Draw(sb, pixel);
            }
        }
    }

    // ================= CHASING ENEMY =================
    public class ChasingEnemy : Enemy
    {
        float chaseSpeed = 100f;
        Player targetPlayer;

        public ChasingEnemy(float x, float y, float speed, float range) : base(x, y, speed, range)
        {
        }

        public void SetTarget(Player player)
        {
            targetPlayer = player;
        }

        public override void Update(GameTime gt, PlayScreen screen)
        {
            if (targetPlayer != null)
            {
                float dt = (float)gt.ElapsedGameTime.TotalSeconds;
                
                // Движемся к игроку
                Vector2 direction = targetPlayer.Position - Position;
                if (direction.Length() > 0)
                {
                    direction.Normalize();
                    Position += direction * chaseSpeed * dt;
                }
            }
            else
            {
                base.Update(gt, screen);
            }
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
                
                if (dist < 35)
                {
                    // Черная дыра - полностью черная
                    data[i] = Color.Black;
                }
                else if (dist < 40)
                {
                    // Край дыры - темно-серый
                    float t = (dist - 35) / 5f;
                    data[i] = Color.Lerp(Color.Black, new Color(20, 20, 20), t);
                }
                else
                {
                    data[i] = Color.Transparent;
                }
            }
            
            tex.SetData(data);
            return tex;
        }

        Texture2D CreatePaperTexture()
        {
            Texture2D tex = new Texture2D(Game1.Instance.GraphicsDevice, 80, 80);
            Color[] data = new Color[80 * 80];
            
            Color paperColor = new Color(250, 245, 230); // Бежевый цвет бумаги
            
            for (int i = 0; i < data.Length; i++)
            {
                int x = i % 80;
                int y = i / 80;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(40, 40));
                
                if (dist > 40)
                {
                    // Бумага с сеткой
                    bool isGridLine = (x % 8 == 0) || (y % 8 == 0);
                    if (isGridLine)
                        data[i] = Color.Lerp(paperColor, Color.Black, 0.1f);
                    else
                        data[i] = paperColor;
                }
                else if (dist > 35)
                {
                    // Рваные края бумаги
                    float angle = (float)Math.Atan2(y - 40, x - 40);
                    float noise = (float)(Math.Sin(angle * 8) * 2 + Math.Cos(angle * 6) * 1.5);
                    float edgeDist = dist - 35 + noise;
                    
                    if (edgeDist > 0 && edgeDist < 3)
                    {
                        // Светлые блики на краях
                        data[i] = Color.Lerp(paperColor, Color.White, 0.7f);
                    }
                    else if (edgeDist < 0)
                    {
                        // Тень на краях
                        data[i] = Color.Lerp(paperColor, Color.Black, 0.3f);
                    }
                    else
                    {
                        data[i] = paperColor;
                    }
                }
                else
                {
                    data[i] = Color.Transparent; // Прозрачно - видна дыра
                }
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
            
            // Притягиваем игрока
            Vector2 toPlayer = screen.Player.Position - Position;
            float distance = toPlayer.Length();
            
            if (distance < pullRadius && distance > 0)
            {
                // Сила притяжения уменьшается с расстоянием
                float pullForce = pullStrength * (1f - distance / pullRadius);
                toPlayer.Normalize();
                
                // Применяем силу притяжения к игроку
                screen.Player.Velocity += toPlayer * pullForce * dt;
                
                // Если игрок слишком близко - Game Over
                if (distance < Size.X / 2)
                {
                    screen.GameOver();
                }
            }
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            // Рисуем бумагу с дырой
            float scale = 1f + (float)(Math.Sin(pulse) * 0.1f);
            Vector2 center = new Vector2(Position.X + Size.X / 2, Position.Y + Size.Y / 2);
            Vector2 origin = new Vector2(40, 40);
            
            // Рисуем бумагу
            Rectangle paperRect = new Rectangle((int)Position.X, (int)Position.Y, (int)(Size.X * scale), (int)(Size.Y * scale));
            sb.Draw(paperTexture, paperRect, null, Color.White, rotation, origin, SpriteEffects.None, 0f);
            
            // Рисуем черную дыру
            float holeScale = 1f + (float)(Math.Sin(pulse * 1.5f) * 0.05f);
            Rectangle holeRect = new Rectangle(
                (int)(Position.X + (Size.X - Size.X * holeScale) / 2), 
                (int)(Position.Y + (Size.Y - Size.Y * holeScale) / 2), 
                (int)(Size.X * holeScale), 
                (int)(Size.Y * holeScale)
            );
            sb.Draw(holeTexture, holeRect, null, Color.White, -rotation * 0.5f, origin, SpriteEffects.None, 0f);
            
            // Эффект свечения вокруг дыры
            float glowAlpha = (float)(Math.Sin(pulse * 2) * 0.3 + 0.2);
            for (int i = 0; i < 3; i++)
            {
                float glowRadius = 40 + i * 5;
                Rectangle glowRect = new Rectangle(
                    (int)(center.X - glowRadius), 
                    (int)(center.Y - glowRadius),
                    (int)(glowRadius * 2),
                    (int)(glowRadius * 2)
                );
                Color glowColor = new Color(50, 0, 100, (int)(glowAlpha * 255 / (i + 1)));
                sb.Draw(pixel, glowRect, glowColor);
            }
        }

        public new bool CheckCollision(Player player)
        {
            // Коллизия обрабатывается в Update через притяжение
            return false;
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

        public bool CheckCollision(Player player)
        {
            return Bounds.Intersects(player.Bounds);
        }
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
                
                if (dist < 10)
                    data[i] = Color.Gold;
                else if (dist < 11)
                    data[i] = Color.Yellow;
                else
                    data[i] = Color.Transparent;
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

        public ParticleSystem(Texture2D pixelTexture)
        {
            pixel = pixelTexture;
        }

        public void CreateJumpEffect(Vector2 position, Color color, int count = 8)
        {
            Random rnd = new Random();
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
            Random rnd = new Random();
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
            Random rnd = new Random();
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
                particle.Velocity.Y += 200f * dt; // Гравитация
                particle.Rotation += particle.RotationSpeed * dt;
                
                if (!particle.IsAlive)
                {
                    particles.Remove(particle);
                }
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
        float comboTimeLimit = 2f; // Время для поддержания комбо
        float lastJumpTime = 0f;

        public void OnJump(float currentTime)
        {
            if (currentTime - lastJumpTime < comboTimeLimit)
            {
                CurrentCombo++;
                if (CurrentCombo > MaxCombo)
                    MaxCombo = CurrentCombo;
            }
            else
            {
                CurrentCombo = 1;
            }
            
            lastJumpTime = currentTime;
            comboTimer = comboTimeLimit;
        }

        public void Update(GameTime gt)
        {
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;
            comboTimer -= dt;
            
            if (comboTimer <= 0)
            {
                CurrentCombo = 0;
            }
        }

        public int GetBonusScore()
        {
            if (CurrentCombo >= 10)
                return CurrentCombo * 5; // x5 за комбо 10+
            else if (CurrentCombo >= 5)
                return CurrentCombo * 3; // x3 за комбо 5-9
            else if (CurrentCombo >= 3)
                return CurrentCombo * 2; // x2 за комбо 3-4
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
        Random rnd = new Random();

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
                // Генерируем метеориты
                if (rnd.Next(0, 100) < 15) // 15% шанс на каждом кадре
                {
                    float spawnX = rnd.Next(0, screen.ViewWidth);
                    float spawnY = screen.Player.Position.Y - screen.ViewHeight;
                    screen.Enemies.Add(new Meteor(spawnX, spawnY));
                }
            }
            else if (Type == SpecialEventType.Wind)
            {
                // Ветер влияет на движение игрока
                // Применяется в Player.Update
            }

            if (Timer <= 0)
            {
                Type = SpecialEventType.None;
            }
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
                    // Оранжево-красный метеорит
                    float t = dist / 18f;
                    data[i] = Color.Lerp(Color.Orange, Color.DarkRed, t);
                }
                else if (dist < 20)
                {
                    // Светящийся край
                    data[i] = Color.Yellow;
                }
                else
                {
                    data[i] = Color.Transparent;
                }
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
            
            // Светящийся след
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
        float spawnHeight;

        public Boss(float x, float y, float speed, float range) : base(x, y, speed, range)
        {
            Size = new Vector2(120, 120); // Большой размер
            moveSpeed = speed;
            moveRange = range;
            startX = x;
            spawnHeight = y;
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
                
                // Основное тело босса
                if (dist < 50)
                {
                    float t = dist / 50f;
                    data[i] = Color.Lerp(new Color(200, 0, 0), new Color(100, 0, 0), t);
                }
                // Глаза
                else if ((x >= 40 && x <= 50 && y >= 45 && y <= 55) || 
                         (x >= 70 && x <= 80 && y >= 45 && y <= 55))
                {
                    data[i] = Color.Yellow;
                }
                // Рот
                else if (x >= 50 && x <= 70 && y >= 70 && y <= 80)
                {
                    data[i] = Color.DarkRed;
                }
                // Ореол опасности
                else if (dist >= 50 && dist < 55)
                {
                    data[i] = new Color(255, 100, 0, 200);
                }
                else
                {
                    data[i] = Color.Transparent;
                }
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

            // Движение влево-вправо
            if (movingRight)
            {
                Position.X += moveSpeed * dt;
                if (Position.X >= startX + moveRange)
                {
                    Position.X = startX + moveRange;
                    movingRight = false;
                }
            }
            else
            {
                Position.X -= moveSpeed * dt;
                if (Position.X <= startX)
                {
                    Position.X = startX;
                    movingRight = true;
                }
            }

            // Атака
            attackTimer += dt;
            if (attackTimer >= attackInterval)
            {
                attackTimer = 0f;
                // Стреляем в сторону игрока
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

            // Обновляем пули
            foreach (var bullet in Bullets.ToList())
            {
                bullet.Update(gt, screen);
                if (bullet.Position.Y > screen.CameraY + screen.ViewHeight + 200 ||
                    bullet.Position.Y < screen.CameraY - 200 ||
                    bullet.Position.X < -50 || bullet.Position.X > screen.ViewWidth + 50)
                {
                    Bullets.Remove(bullet);
                }
            }
        }

        public override void Draw(SpriteBatch sb, Texture2D pixel)
        {
            if (Health <= 0) return;

            // Эффект пульсации
            float scale = 1f + (float)(Math.Sin(pulse) * 0.1f);
            Vector2 center = new Vector2(Position.X + Size.X / 2, Position.Y + Size.Y / 2);
            Rectangle bossRect = new Rectangle(
                (int)(Position.X + (Size.X - Size.X * scale) / 2),
                (int)(Position.Y + (Size.Y - Size.Y * scale) / 2),
                (int)(Size.X * scale),
                (int)(Size.Y * scale)
            );

            // Ореол опасности
            float glowAlpha = (float)(Math.Sin(pulse * 2) * 0.3 + 0.5);
            for (int i = 0; i < 3; i++)
            {
                float glowRadius = 60 + i * 10;
                Rectangle glowRect = new Rectangle(
                    (int)(center.X - glowRadius),
                    (int)(center.Y - glowRadius),
                    (int)(glowRadius * 2),
                    (int)(glowRadius * 2)
                );
                Color glowColor = new Color(255, 100, 0, (int)(glowAlpha * 100 / (i + 1)));
                sb.Draw(pixel, glowRect, glowColor);
            }

            // Тело босса
            sb.Draw(bossTexture, bossRect, null, Color.White, rotation, new Vector2(60, 60), SpriteEffects.None, 0f);

            // Полоса здоровья
            int barWidth = 100;
            int barHeight = 8;
            Rectangle healthBg = new Rectangle(
                (int)(center.X - barWidth / 2),
                (int)(Position.Y - 20),
                barWidth,
                barHeight
            );
            sb.Draw(pixel, healthBg, new Color(50, 0, 0, 200));

            float healthPercent = (float)Health / MaxHealth;
            Rectangle healthBar = new Rectangle(
                (int)(center.X - barWidth / 2),
                (int)(Position.Y - 20),
                (int)(barWidth * healthPercent),
                barHeight
            );
            Color healthColor = healthPercent > 0.5f ? Color.Green :
                               healthPercent > 0.25f ? Color.Yellow : Color.Red;
            sb.Draw(pixel, healthBar, healthColor);

            // Рисуем пули
            foreach (var bullet in Bullets)
            {
                bullet.Draw(sb, pixel);
            }
        }

        public new bool CheckCollision(Player player)
        {
            // Босс не убивает сразу, но наносит урон при столкновении
            if (Bounds.Intersects(player.Bounds))
            {
                if (!player.HasInvincibility && !player.HasShield)
                {
                    // Игрок получает урон, но не умирает сразу
                    // Можно добавить систему здоровья игрока позже
                    return true;
                }
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

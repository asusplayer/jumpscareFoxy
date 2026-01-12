using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace jumpscareFoxy;

[BepInPlugin("com.olivr.jumpscareFoxy", "JumpscareFoxy", "1.5.0")]
public class jumpscareFoxy : BaseUnityPlugin
{
    internal static jumpscareFoxy Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }
    
    private JumpscareManager? jumpscareManager;
    private Configuration? config;

    private void Awake()
    {
        Instance = this;
        
        // Prevent the plugin from being deleted
        this.gameObject.transform.parent = null;
        this.gameObject.hideFlags = HideFlags.HideAndDontSave;

        Logger.LogInfo($"Initializing Jumpscare Mod");
        
        config = new Configuration(Config);
        if (config.Enabled)
        {
            jumpscareManager = new JumpscareManager(config);
        }

        Patch();

        Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
    }

    internal void Patch()
    {
        Harmony ??= new Harmony(Info.Metadata.GUID);
        Harmony.PatchAll();
    }

    internal void Unpatch()
    {
        Harmony?.UnpatchSelf();
    }

    private void Update()
    {
        jumpscareManager?.Update();
    }
    
    public Coroutine StartManagedCoroutine(IEnumerator coroutine)
    {
        return StartCoroutine(coroutine);
    }
}

public class Configuration
{
    public bool Enabled { get; }
    public int Probability { get; }
    public bool ManualTriggerEnabled { get; }
    public KeyCode TriggerKey { get; }
    public int AnimationFPS { get; }

    public Configuration(ConfigFile config)
    {
        Enabled = config.Bind("General", "Enabled", true, "Enable/disable the mod").Value;
        Probability = config.Bind("General", "Probability", 1000, 
            new ConfigDescription("1/X chance per second", 
                new AcceptableValueRange<int>(10, 100000))).Value;
        ManualTriggerEnabled = config.Bind("Controls", "ManualTrigger", true, "Enable manual trigger").Value;
        TriggerKey = config.Bind("Controls", "TriggerKey", KeyCode.J, "Manual trigger key").Value;
        AnimationFPS = config.Bind("Animation", "FPS", 15, 
            new ConfigDescription("Animation speed", 
                new AcceptableValueRange<int>(5, 60))).Value;
    }
}

public class JumpscareManager
{
    private readonly Configuration config;
    private readonly System.Random random = new();
    private float timer;
    private bool isJumpscarePlaying;
    private UIManager? uiManager;

    public JumpscareManager(Configuration config)
    {
        this.config = config;
        uiManager = new UIManager();
        jumpscareFoxy.Logger.LogInfo("Jumpscare manager initialized");
    }

    public void Update()
    {
        if (!isJumpscarePlaying)
        {
            HandleTimedTrigger();
            HandleManualTrigger();
        }
    }

    private void HandleTimedTrigger()
    {
        timer += Time.deltaTime;
        if (timer >= 1f)
        {
            timer = 0f;
            if (random.Next(config.Probability) == 0)
            {
                TriggerJumpscare();
            }
        }
    }

    private void HandleManualTrigger()
    {
        if (config.ManualTriggerEnabled && Input.GetKeyDown(config.TriggerKey))
        {
            TriggerJumpscare();
        }
    }

    private void TriggerJumpscare()
    {
        if (!isJumpscarePlaying)
        {
            jumpscareFoxy.Logger.LogInfo("Triggering jumpscare");
            isJumpscarePlaying = true;
            uiManager?.PlayJumpscare(config.AnimationFPS, () =>
            {
                isJumpscarePlaying = false;
                jumpscareFoxy.Logger.LogInfo("Jumpscare completed");
            });
        }
    }
}

public class UIManager : MonoBehaviour
{
    private GameObject? canvasObject;
    private Image? imageComponent;
    private AudioSource? audioSource;
    private List<Sprite> frames = new();
    private bool assetsLoaded;
    private bool initialized;
    private bool isPlaying;
    private string? assetsRoot;

    public UIManager()
    {
        jumpscareFoxy.Logger.LogInfo("Creating UI Manager");
        jumpscareFoxy.Instance.StartManagedCoroutine(Initialize());
    }

    private IEnumerator Initialize()
    {
        yield return new WaitForEndOfFrame();

        // Get the directory where this mod's DLL is located
        string modDirectory = Path.GetDirectoryName(typeof(jumpscareFoxy).Assembly.Location);
        assetsRoot = Path.Combine(modDirectory!, "assets");

        if (!Directory.Exists(assetsRoot))
        {
            jumpscareFoxy.Logger.LogError($"Assets folder not found at: {assetsRoot}");
            jumpscareFoxy.Logger.LogError("The mod will not work correctly. Please ensure the 'assets' folder is in the same directory as the DLL.");
            yield break;
        }

        jumpscareFoxy.Logger.LogInfo($"Assets root directory detected: {assetsRoot}");

        CreateUI();
        LoadAssets();
        initialized = true;
    }

    private void CreateUI()
    {
        canvasObject = new GameObject("JumpscareCanvas");
        UnityEngine.Object.DontDestroyOnLoad(canvasObject);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        audioSource = canvasObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = 1f;

        GameObject imageObject = new GameObject("JumpscareImage");
        imageObject.transform.SetParent(canvasObject.transform);

        imageComponent = imageObject.AddComponent<Image>();
        imageComponent.preserveAspect = true;
        imageComponent.color = new Color(1f, 1f, 1f, 0f);

        RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        jumpscareFoxy.Logger.LogInfo("UI created successfully");
    }

    private void LoadAssets()
    {
        jumpscareFoxy.Logger.LogInfo("Loading assets...");
        LoadAnimationFrames();
        assetsLoaded = true;
    }

    private void LoadAnimationFrames()
    {
        string framesPath = Path.Combine(assetsRoot!, "frames");

        if (!Directory.Exists(framesPath))
        {
            jumpscareFoxy.Logger.LogError($"Frames directory not found: {framesPath}");
            return;
        }

        string[] files = Directory.GetFiles(framesPath, "*.png").OrderBy(f => f).ToArray();

        if (files.Length == 0)
        {
            jumpscareFoxy.Logger.LogError("No animation frames found!");
            return;
        }

        jumpscareFoxy.Logger.LogInfo($"Loading {files.Length} animation frames...");

        foreach (string path in files)
        {
            try
            {
                byte[] data = File.ReadAllBytes(path);
                Texture2D texture = new(2, 2);

                if (ImageConversion.LoadImage(texture, data))
                {
                    frames.Add(Sprite.Create(texture, 
                        new Rect(0f, 0f, texture.width, texture.height), 
                        new Vector2(0.5f, 0.5f), 100f));
                }
            }
            catch (Exception ex)
            {
                jumpscareFoxy.Logger.LogError($"Error loading frame: {ex.Message}");
            }
        }

        jumpscareFoxy.Logger.LogInfo($"Loaded {frames.Count} frames");
    }

    public void PlayJumpscare(int fps, Action onComplete)
    {
        if (!initialized || !assetsLoaded)
        {
            jumpscareFoxy.Logger.LogWarning("UI not ready or assets not loaded");
            return;
        }

        if (imageComponent == null)
        {
            jumpscareFoxy.Logger.LogError("Image component missing");
            return;
        }

        if (isPlaying)
        {
            return;
        }

        isPlaying = true;
        imageComponent.color = Color.white;
        jumpscareFoxy.Instance.StartManagedCoroutine(JumpscareSequence(fps, onComplete));
    }

    private IEnumerator JumpscareSequence(int fps, Action onComplete)
    {
        yield return LoadAndPlayAudio();

        if (frames.Count > 0)
        {
            float delay = 1f / fps;
            foreach (Sprite frame in frames)
            {
                imageComponent!.sprite = frame;
                yield return new WaitForSeconds(delay);
            }
        }
        else
        {
            jumpscareFoxy.Logger.LogWarning("No frames available, using fallback");
            yield return new WaitForSeconds(2f);
        }

        imageComponent!.color = new Color(1f, 1f, 1f, 0f);
        isPlaying = false;
        onComplete?.Invoke();
    }

    private IEnumerator LoadAndPlayAudio()
    {
        string soundPath = Path.Combine(assetsRoot!, "jumpscare.wav");

        if (!File.Exists(soundPath))
        {
            jumpscareFoxy.Logger.LogError($"Audio file not found: {soundPath}");
            yield break;
        }

        using (WWW www = new WWW("file://" + soundPath))
        {
            yield return www;

            if (!string.IsNullOrEmpty(www.error))
            {
                jumpscareFoxy.Logger.LogError($"Audio load error: {www.error}");
            }
            else
            {
                audioSource!.clip = www.GetAudioClip(false, false, AudioType.WAV);
                if (audioSource.clip != null)
                {
                    audioSource.Play();
                    jumpscareFoxy.Logger.LogInfo("Playing jumpscare sound");
                }
                else
                {
                    jumpscareFoxy.Logger.LogError("Failed to create audio clip");
                }
            }
        }
    }
}
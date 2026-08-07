using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Eggverse
{
    public enum GameMode { Title, Space, Surface, Battle, Victory }

    /// <summary>Owns the game: builds the world, switches modes, runs the story, drives the camera.</summary>
    public class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        public GameState State { get; private set; }
        public StoryState Story { get; private set; }
        public Camera Cam { get; private set; }
        public TeoController Teo { get; private set; }
        public SpaceMode Space { get; private set; }
        public SurfaceMode Surface { get; private set; }
        public BattleMode Battle { get; private set; }
        public HudView Hud { get; private set; }
        public DialogueView Dialogue { get; private set; }
        public GalaxyMapView Map { get; private set; }
        public AudioDirector Audio { get; private set; }
        public TransitionView Transition { get; private set; }
        public NameEntryView NameEntry { get; private set; }
        public PauseView Pause { get; private set; }

        public GameMode Mode { get; private set; }
        public PlanetDef CurrentPlanet { get; private set; }

        TrainerDef pendingTrainer;
        Vector3 camVelocity;
        float targetOrthoSize = 15f;
        SaveData pendingSave;
        float playSeconds;

        const float SpaceZoom = 15f;
        const float SurfaceZoom = 10f;

        public bool OverlayOpen => (Hud != null && Hud.CollectionOpen)
                                || (Map != null && Map.IsOpen)
                                || (Dialogue != null && Dialogue.IsOpen)
                                || (Pause != null && Pause.IsOpen)
                                || (NameEntry != null && NameEntry.IsOpen);

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            State = new GameState();
            Story = new StoryState();

            Audio = Attach<AudioDirector>("Audio");
            Audio.Build();

            SetupCamera();
            SetupEventSystem();

            var teoGo = new GameObject("Teo");
            teoGo.transform.SetParent(transform, false);
            Teo = teoGo.AddComponent<TeoController>();
            Teo.Build();

            Space = Attach<SpaceMode>("SpaceMode");
            Space.Build(this);

            Surface = Attach<SurfaceMode>("SurfaceMode");
            Surface.Build(this);

            Battle = Attach<BattleMode>("BattleMode");
            Battle.Build(this);

            Hud = Attach<HudView>("HudView");
            Hud.Build(this);

            Dialogue = Attach<DialogueView>("DialogueView");
            Dialogue.Build(this);

            Map = Attach<GalaxyMapView>("GalaxyMap");
            Map.Build(this);

            NameEntry = Attach<NameEntryView>("NameEntry");
            NameEntry.Build();

            Pause = Attach<PauseView>("Pause");
            Pause.Build(this);

            Transition = Attach<TransitionView>("Transition");
            Transition.Build();

            Story.BeatAdvanced += OnBeatAdvanced;
        }

        T Attach<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        void Start()
        {
            pendingSave = SaveSystem.Peek();

            CurrentPlanet = PlanetDatabase.Home;
            State.Visited.Add(CurrentPlanet.Id);
            Mode = GameMode.Title;
            Teo.Movement = TeoMovement.Frozen;

            // Stage the home planet behind the title card so the first frame is not empty.
            Space.SetActive(false);
            Surface.SetActive(true);
            Surface.Enter(CurrentPlanet);
            Teo.SurfaceRadius = CurrentPlanet.SurfaceRadius;
            Teo.Warp(Surface.LandingPoint());
            Cam.transform.position = new Vector3(0f, 0f, -10f);
            Cam.orthographicSize = SurfaceZoom;
            targetOrthoSize = SurfaceZoom;

            Hud.SetHudVisible(false);
            Hud.SetTitleSave(SaveSystem.Describe(pendingSave));
            Audio.SetMusic(MusicTrack.Explore, 2.5f);
        }

        void UpdateMusic()
        {
            MusicTrack want;
            if (Mode == GameMode.Battle) want = MusicTrack.Battle;
            else if (CurrentPlanet == null) want = MusicTrack.Explore;
            else if (CurrentPlanet.IsBossWorld) want = MusicTrack.Amaranth;
            // The Belt gets its own bed, so crossing into Sector III is audible.
            else if (CurrentPlanet.Sector == Sector.ShatteredBelt) want = MusicTrack.Belt;
            else want = MusicTrack.Explore;
            Audio.SetMusic(want);
        }

        // ==================================================================
        // saving
        // ==================================================================

        /// <summary>
        /// Pushes the screen-motion setting out to the things that move. Called when it changes
        /// and when a run starts or loads, rather than before each of the five flash sites.
        /// </summary>
        /// <summary>
        /// Says something the moment the record crosses one of Ori's thresholds. He has a reward
        /// waiting at each, but only hands it over when you next stand in front of him - so
        /// crossing ten species on a rock three sectors out used to pass in complete silence.
        /// </summary>
        void NudgeRecordMilestone()
        {
            int recorded = State.Caught.Count;
            int catchable = SpeciesDatabase.CatchableCount;

            if (recorded >= catchable && !Story.HasFlag("ori_record_full"))
                Hud.Toast("The record is complete. All " + catchable + " of them. Ori will not believe it.");
            else if (recorded >= StoryDatabase.RecordNoticeSecond && !Story.HasFlag("ori_record_18"))
                Hud.Toast(Words.SpellCapitalised(StoryDatabase.RecordNoticeSecond) +
                          " species recorded. Ori would want to see that.");
            else if (recorded >= StoryDatabase.RecordNoticeFirst && !Story.HasFlag("ori_record_10"))
                Hud.Toast(Words.SpellCapitalised(StoryDatabase.RecordNoticeFirst) +
                          " species recorded. Ori would want to see that.");
        }

        public void ApplyMotionSetting()
        {
            if (Transition != null) Transition.Reduced = !State.ScreenMotion;
        }

        public void SaveNow(bool announce = false)
        {
            if (Mode == GameMode.Title) return;
            bool ok = SaveSystem.Save(State, Story, CurrentPlanet != null ? CurrentPlanet.Id : PlanetDatabase.Home.Id, playSeconds);
            if (announce)
            {
                Hud.Toast(ok ? "Progress saved." : "Could not write the save file.");
                if (ok) Audio.Play(Sfx.Save);
            }
            else if (ok)
            {
                // The autosaves used to pass in silence. A corner mark is enough to say the run
                // is safe without interrupting whatever the player was doing to earn it.
                Hud.ShowAutosaved();
            }
            else
            {
                // A failed autosave is the one case worth interrupting for: the player would
                // otherwise keep playing believing the run is being written down.
                Hud.Toast("Could not write the save file. Your progress is not being saved.");
            }
        }

        void LoadSavedGame()
        {
            GameState loadedState;
            StoryState loadedStory;
            string planetId;
            float seconds;

            if (!SaveSystem.Load(out loadedState, out loadedStory, out planetId, out seconds))
            {
                Hud.Toast("That save could not be read. Starting a new run.");
                StartFreshGame();
                return;
            }

            Story.BeatAdvanced -= OnBeatAdvanced;
            State = loadedState;
            Story = loadedStory;
            Story.BeatAdvanced += OnBeatAdvanced;
            playSeconds = seconds;

            Hud.Rebind(State);

            CurrentPlanet = PlanetDatabase.Get(planetId);
            State.CurrentPlanetId = CurrentPlanet.Id;

            Space.SetActive(false);
            Surface.SetActive(true);
            Surface.Enter(CurrentPlanet);
            Teo.SurfaceRadius = CurrentPlanet.SurfaceRadius;
            Teo.Warp(Surface.LandingPoint());

            Mode = GameMode.Surface;
            Teo.Movement = TeoMovement.Walking;
            targetOrthoSize = SurfaceZoom;
            Cam.transform.position = new Vector3(Teo.transform.position.x, Teo.transform.position.y, -10f);

            Hud.HideTitle();
            Hud.SetHudVisible(true);
            Hud.Refresh();
            // What is left, not what the objective said. Coming back to a run after a week, the
            // useful sentence is the one naming the thing you have not done - the objective panel
            // is on screen behind this toast and already carries the full wording.
            string outstanding = Story.CurrentBlockerText(State);
            Hud.Toast(outstanding != null
                ? "Welcome back. Still needed: " + outstanding
                : "Welcome back. " + Story.Current.Objective);
        }

        void StartFreshGame()
        {
            SaveSystem.Delete();
            pendingSave = null;

            // Reaching the title via the pause menu leaves the old run live, so a "new run"
            // has to rebuild the state rather than just hiding the title card.
            Story.BeatAdvanced -= OnBeatAdvanced;
            State = new GameState();
            Story = new StoryState();
            Story.BeatAdvanced += OnBeatAdvanced;
            playSeconds = 0f;
            Hud.Rebind(State);

            CurrentPlanet = PlanetDatabase.Home;
            State.CurrentPlanetId = CurrentPlanet.Id;

            Space.SetActive(false);
            Surface.SetActive(true);
            Surface.Enter(CurrentPlanet);
            Teo.SurfaceRadius = CurrentPlanet.SurfaceRadius;
            Teo.Warp(Surface.LandingPoint());
            targetOrthoSize = SurfaceZoom;
            Cam.transform.position = new Vector3(Teo.transform.position.x, Teo.transform.position.y, -10f);

            BeginGame();
        }

        /// <summary>Saves, then drops back to the title card so the run can be resumed or restarted.</summary>
        public void ReturnToTitle()
        {
            SaveNow();
            pendingSave = SaveSystem.Peek();

            Mode = GameMode.Title;
            Teo.Movement = TeoMovement.Frozen;
            Map.Close();
            Hud.CloseCollection();
            Hud.HideVictory();
            Hud.SetHudVisible(false);
            Hud.ShowTitle();
            Hud.SetTitleSave(SaveSystem.Describe(pendingSave));
            Transition.FlashDark(0.6f);

            // PauseView and GameDirector both run Update with no guaranteed order, so the
            // Enter that confirmed the quit could otherwise be read again this same frame
            // and bounce straight back into the run.
            modeGrace = 0.35f;
        }

        float modeGrace;

        void SetupCamera()
        {
            Cam = Camera.main;
            if (Cam == null) Cam = Object.FindAnyObjectByType<Camera>();
            if (Cam == null)
            {
                var go = new GameObject("Main Camera", typeof(Camera));
                go.tag = "MainCamera";
                Cam = go.GetComponent<Camera>();
            }
            Cam.orthographic = true;
            Cam.orthographicSize = SpaceZoom;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color32(0x05, 0x06, 0x0E, 0xFF);
            Cam.transform.position = new Vector3(0f, 0f, -10f);
            Cam.transform.rotation = Quaternion.identity;
        }

        void SetupEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.transform.SetParent(transform, false);
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        // ==================================================================
        // mode changes
        // ==================================================================

        public void BeginGame()
        {
            Mode = GameMode.Surface;
            Teo.Movement = TeoMovement.Walking;
            Hud.HideTitle();
            Hud.SetHudVisible(true);
            Hud.Refresh();
            // Generated, so it follows the database rather than restating it.
            Hud.Toast(PlanetDatabase.Home.Name + ". Ori is at the Nest Station.");
        }

        public void Land(PlanetDef planet)
        {
            bool firstVisit = State.Visited.Add(planet.Id);

            CurrentPlanet = planet;
            State.CurrentPlanetId = planet.Id;

            Space.SetActive(false);
            Surface.SetActive(true);
            Surface.Enter(planet);

            Teo.SurfaceRadius = planet.SurfaceRadius;
            Teo.Warp(Surface.LandingPoint());
            Teo.Movement = TeoMovement.Walking;
            Teo.ClearPuffs();

            Mode = GameMode.Surface;
            targetOrthoSize = SurfaceZoom;
            Cam.transform.position = new Vector3(Teo.transform.position.x, Teo.transform.position.y, -10f);

            Hud.Refresh();
            // A tagline is what a place is; it belongs to arriving somewhere for the first time.
            // On the twentieth return it is noise, and what a returning collector actually wants
            // to know is whether this world still owes them anything.
            Hud.Toast(firstVisit
                ? "Charted " + planet.Name + ". " + planet.Tagline
                : planet.Name + ". " + StillOwed(planet));
            Audio.Play(Sfx.Land);
            Transition.Flash(planet.Atmosphere * 0.5f, 0.55f);
            SaveNow();
        }

        /// <summary>
        /// What this world still has that the player has not recorded. Read in three places -
        /// the chart, the approach prompt, and the toast on landing - so they cannot disagree.
        /// </summary>
        public string StillOwed(PlanetDef planet)
        {
            int missing = 0;
            for (int i = 0; i < planet.Spawns.Length; i++)
            {
                var sp = SpeciesDatabase.Get(planet.Spawns[i].SpeciesId);
                if (sp.CatchRate >= 20 && !State.Caught.Contains(sp.Id)) missing++;
            }
            if (missing == 0) return "Every egg here is already in your record.";
            return Words.Count(missing, "egg") + " here you have not recorded yet.";
        }

        public void LiftOff()
        {
            Surface.SetActive(false);
            Space.SetActive(true);

            Teo.Warp(Space.DeparturePoint(CurrentPlanet));
            Teo.Movement = TeoMovement.Flying;

            Mode = GameMode.Space;
            targetOrthoSize = SpaceZoom;
            Cam.transform.position = new Vector3(Teo.transform.position.x, Teo.transform.position.y, -10f);

            Hud.Refresh();
            Hud.Toast("Back in the black. M opens the chart.");
            Audio.Play(Sfx.Liftoff);
            Transition.FlashDark(0.5f);
        }

        /// <summary>Jumps to orbit above an already-charted world. The player still lands themselves.</summary>
        public void FastTravel(PlanetDef planet)
        {
            Surface.SetActive(false);
            Space.SetActive(true);

            CurrentPlanet = planet;
            Teo.Warp(Space.DeparturePoint(planet));
            Teo.Movement = TeoMovement.Flying;
            Teo.ClearPuffs();

            Mode = GameMode.Space;
            targetOrthoSize = SpaceZoom;
            Cam.transform.position = new Vector3(Teo.transform.position.x, Teo.transform.position.y, -10f);

            Hud.Refresh();
            Hud.Toast("Course set. You are in orbit above " + planet.Name + ".");
            Audio.Play(Sfx.Liftoff);
            Transition.FlashDark(0.6f);
        }

        // ==================================================================
        // story and dialogue
        // ==================================================================

        void OnBeatAdvanced(StoryBeat beat)
        {
            Hud.Toast(beat.Chapter + " — " + beat.Objective);
            Hud.Refresh();
            SaveNow();
        }

        public void PlayDialogue(DialogueScript script)
        {
            if (script == null || Dialogue.IsOpen) return;
            Hud.SetPrompt(null);
            Teo.Movement = TeoMovement.Frozen;
            Dialogue.Play(script, () => OnDialogueComplete(script));
        }

        void OnDialogueComplete(DialogueScript script)
        {
            if (!string.IsNullOrEmpty(script.SetsFlag)) Story.SetFlag(script.SetsFlag);

            if (script.HealsParty)
            {
                State.RestoreEggs();
                Hud.Toast("Your nest is warm again.");
            }
            if (script.RestocksCartons)
            {
                State.RestockSupplies();
                Hud.Toast("Cartons restocked.");
            }

            if (!string.IsNullOrEmpty(script.GivesSpeciesId))
            {
                var gift = EggInstance.Wild(script.GivesSpeciesId, script.GivesSpeciesLevel);
                State.Collect(gift);
                Hud.Toast("You received " + gift.Name + "!");
            }

            Story.Evaluate(State);
            State.RaiseChanged();

            if (!string.IsNullOrEmpty(script.StartsTrainer))
            {
                var trainer = StoryDatabase.GetTrainer(script.StartsTrainer);
                if (trainer != null) { BeginTrainerBattle(trainer); return; }
            }

            Teo.Movement = Mode == GameMode.Space ? TeoMovement.Flying : TeoMovement.Walking;
            SaveNow();
        }

        // ==================================================================
        // battles
        // ==================================================================

        public void BeginWildBattle(EggInstance foe)
        {
            if (Mode == GameMode.Battle) return;
            pendingTrainer = null;
            EnterBattle(new List<EggInstance> { foe }, null);
        }

        public void BeginTrainerBattle(TrainerDef trainer)
        {
            if (Mode == GameMode.Battle || trainer == null) return;
            pendingTrainer = trainer;

            var team = new List<EggInstance>();
            for (int i = 0; i < trainer.SpeciesIds.Length; i++)
            {
                int level = i < trainer.Levels.Length ? trainer.Levels[i] : trainer.Levels[trainer.Levels.Length - 1];
                team.Add(EggInstance.Wild(trainer.SpeciesIds[i], level));
            }
            EnterBattle(team, trainer.Name);
        }

        /// <summary>Amy, reached by walking up to her on Amaranth Prime.</summary>
        public void BeginBossBattle()
        {
            BeginTrainerBattle(StoryDatabase.GetTrainer("amy"));
        }

        void EnterBattle(List<EggInstance> team, string opponentName)
        {
            Mode = GameMode.Battle;
            Teo.Movement = TeoMovement.Frozen;
            Hud.CloseCollection();
            Map.Close();
            Hud.SetHudVisible(false);
            Audio.Play(Sfx.Encounter);
            Transition.FlashDark(0.45f);
            UpdateMusic();
            Battle.Begin(team, opponentName);
        }

        public void OnBattleFinished(BattleOutcome outcome)
        {
            var trainer = pendingTrainer;
            pendingTrainer = null;

            Surface.NotifyBattleEnded();
            Hud.SetHudVisible(true);
            Mode = GameMode.Surface;

            switch (outcome)
            {
                case BattleOutcome.Caught:
                    // The battle screen has already said "Gotcha!". The useful thing left to say
                    // is where the egg went - a player who does not know it went to the nest will
                    // not think to go and trade it in - and whether the record just grew.
                    {
                        string who = string.IsNullOrEmpty(Battle.LastCatchName) ? "It" : Battle.LastCatchName;
                        string where = Battle.LastCatchJoinedParty
                            ? who + " joined your party."
                            : who + " is waiting at the nest.";
                        if (Battle.LastCatchWasElder) where = "An Elder! " + where;
                        if (Battle.LastCatchWasNewSpecies) where += "  New to the record.";
                        Hud.Toast(where);
                        if (Battle.LastCatchWasNewSpecies) NudgeRecordMilestone();
                    }
                    break;

                case BattleOutcome.Lost:
                    State.HealAll();
                    Teo.Warp(Surface.LandingPoint());
                    Hud.Toast("You woke up at the Nest Station. Everything is patched up.");
                    break;

                case BattleOutcome.Won:
                    if (trainer != null)
                    {
                        Story.SetFlag(trainer.VictoryFlag);
                        Story.Evaluate(State);
                        State.RaiseChanged();

                        bool wasAmy = trainer.Id == "amy";
                        if (wasAmy) State.AmyDefeated = true;

                        // The real scene happens after the fight.
                        PlayDialogue(new DialogueScript(trainer.OnDefeat, null, null, null, 5, true));
                        if (wasAmy) pendingVictoryScreen = true;
                        return;
                    }
                    break;
            }

            Story.Evaluate(State);
            Teo.Movement = TeoMovement.Walking;
            State.RaiseChanged();
            SaveNow();
        }

        bool pendingVictoryScreen;

        // ==================================================================
        // per-frame
        // ==================================================================

        void Update()
        {
            if (Mode != GameMode.Title) playSeconds += Time.deltaTime;
            UpdateMusic();

            // Swallow input for a moment after a mode change so the keypress that caused it
            // is not immediately re-read by the mode it lands in.
            if (modeGrace > 0f) { modeGrace -= Time.deltaTime; return; }

            switch (Mode)
            {
                case GameMode.Title:
                    if (EggInput.ConfirmPressed)
                    {
                        if (pendingSave != null) LoadSavedGame(); else BeginGame();
                    }
                    else if (pendingSave != null && EggInput.NKeyPressed)
                    {
                        StartFreshGame();
                    }
                    return;

                case GameMode.Victory:
                    if (EggInput.ConfirmPressed)
                    {
                        Hud.HideVictory();
                        Hud.SetHudVisible(true);
                        Mode = GameMode.Surface;
                        Teo.Movement = TeoMovement.Walking;
                    }
                    return;

                case GameMode.Battle:
                    return;
            }

            // A conversation, the namer, or the pause menu owns all input while it is up.
            if (Dialogue.IsOpen || NameEntry.IsOpen || Pause.IsOpen)
            {
                Teo.Movement = TeoMovement.Frozen;
                return;
            }

            if (pendingVictoryScreen)
            {
                pendingVictoryScreen = false;
                Mode = GameMode.Victory;
                Hud.SetHudVisible(false);
                Hud.ShowVictory();
                modeGrace = 0.35f;
                return;
            }

            if (Map.IsOpen)
            {
                if (EggInput.MapPressed || EggInput.CancelPressed) Map.Close();
                Teo.Movement = TeoMovement.Frozen;
                return;
            }

            if (EggInput.MutePressed) { Audio.ToggleMute(); Hud.Toast(Audio.Muted ? "Sound off." : "Sound on."); }

            if (EggInput.MapPressed) { Hud.CloseCollection(); Map.Open(); Audio.Play(Sfx.Chart); return; }

            if (EggInput.PartyPressed) Hud.ToggleCollection();
            else if (Hud.CollectionOpen && EggInput.CancelPressed) Hud.CloseCollection();
            // `overlayWasOpen` is sampled in LateUpdate, so an overlay that closed itself
            // earlier this frame still suppresses the Esc that closed it — whichever order
            // the two Update calls happened to run in.
            else if (EggInput.CancelPressed && !overlayWasOpen) { Pause.Open(); Audio.Play(Sfx.UiConfirm); return; }

            // Reading your collection should not also fly the ship.
            var wanted = OverlayOpen
                ? TeoMovement.Frozen
                : (Mode == GameMode.Space ? TeoMovement.Flying : TeoMovement.Walking);
            if (Teo.Movement != wanted) Teo.Movement = wanted;

            Story.Evaluate(State);
        }

        bool overlayWasOpen;

        void LateUpdate()
        {
            // Sampled after every Update has run, so it reflects the true end-of-frame state.
            overlayWasOpen = OverlayOpen;

            if (Cam == null || Teo == null) return;
            if (Mode == GameMode.Battle || Mode == GameMode.Victory) return;

            Vector3 lead = new Vector3(Teo.Velocity.x, Teo.Velocity.y, 0f) * (Mode == GameMode.Space ? 0.30f : 0.16f);
            Vector3 target = Teo.transform.position + lead;
            target.z = -10f;

            Cam.transform.position = Vector3.SmoothDamp(Cam.transform.position, target, ref camVelocity,
                                                        Mode == GameMode.Space ? 0.18f : 0.12f);
            Cam.orthographicSize = Mathf.Lerp(Cam.orthographicSize, targetOrthoSize,
                                              1f - Mathf.Exp(-5f * Time.deltaTime));
        }
    }
}

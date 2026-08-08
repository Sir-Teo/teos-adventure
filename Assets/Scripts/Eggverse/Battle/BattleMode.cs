using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Eggverse
{
    public enum BattleOutcome { Won, Lost, Fled, Caught }

    enum PlayerAction { Fight, Carton, Salve, Swap, Run }


    /// <summary>Turn-based egg battles, driven by one long coroutine.</summary>
    public class BattleMode : MonoBehaviour
    {
        GameDirector dir;
        GameState State { get { return dir.State; } }

        // ---- ui ----
        Canvas canvas;
        Image foeEggImage, myEggImage;
        Text foeNameText, foeTypeText, myNameText, myTypeText, myHpText, messageText, menuHint;
        Text foeMetaText, myMetaText, foeRecordText;
        Image foeTypeChip, myTypeChip;
        BarWidget foeHpBar, myHpBar, myXpBar;
        RectTransform actionPanel, movePanel, partyPanel, shakeRoot;
        float shakeStrength;
        readonly List<Button> actionButtons = new List<Button>();
        readonly List<Button> moveButtons = new List<Button>();
        readonly List<Button> partyButtons = new List<Button>();

        // ---- menu plumbing ----
        List<Button> activeMenu;
        int menuCols = 2;
        int cursor;
        int pendingChoice = -1;
        bool allowCancel;

        // ---- battle state ----
        readonly List<EggInstance> foeTeam = new List<EggInstance>();
        int foeIndex;
        int activeIndex;
        string trainerName;              // null for a wild encounter
        bool IsTrainer => !string.IsNullOrEmpty(trainerName);
        bool battleOver;
        BattleOutcome outcome;
        int rounds;
        bool foeWasElder;

        /// <summary>What the last catch was, so the toast afterwards can say something the
        /// battle screen did not already say.</summary>
        public string LastCatchName { get; private set; }
        public bool LastCatchJoinedParty { get; private set; }
        /// <summary>How hurt a lead has to be before the game mentions salves, once.</summary>
        public const float SalveHintFraction = 0.45f;

        public bool LastCatchWasNewSpecies { get; private set; }
        public bool LastCatchWasElder { get; private set; }
        PlayerAction chosenAction;
        int chosenParam;
        Coroutine routine;

        EggInstance Foe { get { return foeTeam[foeIndex]; } }
        EggInstance Mine { get { return State.Party[activeIndex]; } }

        static readonly MoveDef Flail = new MoveDef("flail", "Flail", EggType.Plain, 30, 100, 1, MoveEffect.Recoil25, "Out of options.");

        // ==================================================================
        // construction
        // ==================================================================

        public void Build(GameDirector director)
        {
            dir = director;
            canvas = UIKit.CreateCanvas("BattleUI", 30, transform);
            // Everything lives under one node so the whole scene can be shaken as a unit.
            shakeRoot = UIKit.Node((RectTransform)canvas.transform, "Content");
            UIKit.Stretch(shakeRoot, 0, 0, 0, 0);
            var rootRt = shakeRoot;

            var backdrop = UIKit.Panel(rootRt, "Backdrop", new Color32(0x08, 0x0A, 0x14, 0xFA));
            UIKit.Stretch(backdrop.rectTransform, 0, 0, 0, 0);

            var glowTop = UIKit.Picture(rootRt, "GlowTop", ProcArt.Disc("battleglow", new Color(0.35f, 0.28f, 0.6f, 1f), new Color(0.1f, 0.1f, 0.2f, 0f), 1.4f, 128, 64f));
            UIKit.Place(glowTop.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-430f, -330f), new Vector2(900f, 900f));
            glowTop.color = new Color(1f, 1f, 1f, 0.35f);

            var glowBottom = UIKit.Picture(rootRt, "GlowBottom", ProcArt.Disc("battleglow2", new Color(0.25f, 0.45f, 0.55f, 1f), new Color(0.1f, 0.1f, 0.2f, 0f), 1.4f, 128, 64f));
            UIKit.Place(glowBottom.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(440f, 540f), new Vector2(1000f, 1000f));
            glowBottom.color = new Color(1f, 1f, 1f, 0.30f);

            // --- foe ---
            foeEggImage = UIKit.Picture(rootRt, "FoeEgg", ProcArt.White);
            UIKit.Place(foeEggImage.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-430f, -330f), new Vector2(300f, 300f));

            BuildCard(rootRt, "FoeCard", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -70f), new Vector2(660f, 150f),
                      out foeNameText, out foeTypeChip, out foeTypeText, out foeHpBar, out _, out foeMetaText);

            // The foe card deliberately shows no HP numbers, which left 52px of the panel — a
            // third of its height — empty. Whether this species is already in the record is
            // exactly what you want to know before spending a carton on it, so it goes here.
            var foeCard = (RectTransform)foeNameText.transform.parent;
            foeRecordText = UIKit.Label(foeCard, "Record", "", 20, UIKit.Accent, TextAnchor.MiddleLeft);
            UIKit.Place(foeRecordText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(24f, -104f), new Vector2(500f, 26f));

            // --- mine ---
            myEggImage = UIKit.Picture(rootRt, "MyEgg", ProcArt.White);
            UIKit.Place(myEggImage.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(440f, 545f), new Vector2(360f, 360f));

            BuildCard(rootRt, "MyCard", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-70f, -110f), new Vector2(700f, 210f),
                      out myNameText, out myTypeChip, out myTypeText, out myHpBar, out myHpText, out myMetaText);

            var myCard = (RectTransform)myNameText.transform.parent;
            var xpLabel = UIKit.Label(myCard, "XpLabel", "XP", 18, UIKit.InkDim, TextAnchor.MiddleLeft);
            UIKit.Place(xpLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 22f), new Vector2(36f, 24f));
            myXpBar = UIKit.Bar(myCard, "XpBar", new Color32(0x0C, 0x0E, 0x18, 0xFF), new Color32(0x62, 0xC8, 0xF5, 0xFF));
            UIKit.Place(myXpBar.Background.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(66f, 24f), new Vector2(600f, 12f));

            // --- message box ---
            var msgPanel = UIKit.Panel(rootRt, "MessagePanel", UIKit.PanelDark);
            UIKit.Place(msgPanel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(1080f, 210f));
            var msgEdge = UIKit.Panel(msgPanel.transform, "Edge", UIKit.PanelLight);
            UIKit.Place(msgEdge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1080f, 4f));
            messageText = UIKit.Label(msgPanel.transform, "Text", "", 30, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Stretch(messageText.rectTransform, 32f, 44f, 32f, 26f);
            menuHint = UIKit.Label(msgPanel.transform, "Hint", "", 19, UIKit.InkDim, TextAnchor.LowerLeft);
            UIKit.Stretch(menuHint.rectTransform, 32f, 14f, 32f, 26f);

            // --- action menu ---
            // Five actions in a 2x3 grid. A submenu would bury CARTON, which is the verb the
            // whole game is about, so the panel grows instead and the rows tighten to fit.
            actionPanel = BuildMenuPanel(rootRt, "ActionPanel", 260f);
            string[] actions = { "FIGHT", "CARTON", "SALVE", "SWAP", "RUN" };
            for (int i = 0; i < actions.Length; i++)
            {
                int index = i;
                var b = UIKit.TextButton(actionPanel, "Action" + i, actions[i], 26, UIKit.PanelLight, UIKit.Ink, () => Pick(index));
                UIKit.Place(b.image.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(20f + (i % 2) * 350f, -20f - (i / 2) * 78f), new Vector2(330f, 64f));
                actionButtons.Add(b);
            }

            // --- move menu ---
            movePanel = BuildMenuPanel(rootRt, "MovePanel", 210f);
            for (int i = 0; i < EggInstance.MaxMoves; i++)
            {
                int index = i;
                var b = UIKit.TextButton(movePanel, "Move" + i, "-", 24, UIKit.PanelLight, UIKit.Ink, () => Pick(index));
                UIKit.Place(b.image.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(20f + (i % 2) * 350f, -20f - (i / 2) * 90f), new Vector2(330f, 74f));
                moveButtons.Add(b);
            }

            // --- party menu ---
            partyPanel = UIKit.Node(rootRt, "PartyPanel");
            UIKit.Place(partyPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 660f));
            var partyBg = UIKit.Panel(partyPanel, "Bg", UIKit.PanelDark);
            UIKit.Stretch(partyBg.rectTransform, 0, 0, 0, 0);
            var partyTitle = UIKit.Label(partyPanel, "Title", "SEND OUT WHICH EGG?", 30, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(partyTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(820f, 44f));
            for (int i = 0; i < GameState.PartySize; i++)
            {
                int index = i;
                var b = UIKit.TextButton(partyPanel, "Party" + i, "-", 24, UIKit.PanelLight, UIKit.Ink, () => Pick(index));
                UIKit.Place(b.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -90f - i * 88f), new Vector2(820f, 76f));
                UIKit.CaptionOf(b).alignment = TextAnchor.MiddleLeft;
                partyButtons.Add(b);
            }

            canvas.gameObject.SetActive(false);
        }

        RectTransform BuildMenuPanel(Transform parent, string name, float height)
        {
            var panel = UIKit.Node(parent, name);
            UIKit.Place(panel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(740f, height));
            var bg = UIKit.Panel(panel, "Bg", UIKit.PanelDark);
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, 0);
            var edge = UIKit.Panel(panel, "Edge", UIKit.PanelLight);
            UIKit.Place(edge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(740f, 4f));
            panel.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>
        /// One combatant's card. Name and level on the first line, trait and any stat-stage
        /// arrows on a second — cramming all of it onto one line overflows the panel once
        /// names get long ("Elder Frizzlebolt" plus a trait plus arrows is ~700px in 430).
        /// </summary>
        void BuildCard(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
                       out Text nameText, out Image typeChip, out Text typeText, out BarWidget hpBar,
                       out Text hpText, out Text metaText)
        {
            var card = UIKit.Node(parent, name);
            UIKit.Place(card, anchor, pivot, pos, size);
            var bg = UIKit.Panel(card, "Bg", UIKit.PanelMid);
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, 0);

            nameText = UIKit.Label(card, "Name", "", 30, UIKit.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            // 500, not 440. A twelve-character nickname on a three-digit HP at level 30 sat
            // inside 4% of wrapping, and the card is 700 wide with the type chip on the far
            // right - the name had 60 pixels of clear space it was not using.
            UIKit.Place(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -14f), new Vector2(500f, 36f));

            typeChip = UIKit.Panel(card, "TypeChip", Color.gray);
            UIKit.Place(typeChip.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -14f), new Vector2(150f, 32f));
            typeText = UIKit.Label(typeChip.transform, "Text", "", 20, new Color(0.08f, 0.08f, 0.12f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(typeText.rectTransform, 4, 2, 4, 2);

            metaText = UIKit.Label(card, "Meta", "", 18, UIKit.InkDim, TextAnchor.MiddleLeft);
            UIKit.Place(metaText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -52f), new Vector2(size.x - 48f, 24f));

            hpBar = UIKit.Bar(card, "HpBar", new Color32(0x0C, 0x0E, 0x18, 0xFF), UIKit.Good);
            UIKit.Place(hpBar.Background.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -80f), new Vector2(size.x - 48f, 18f));

            hpText = UIKit.Label(card, "HpText", "", 22, UIKit.InkDim, TextAnchor.MiddleRight);
            UIKit.Place(hpText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -104f), new Vector2(300f, 26f));
        }

        // ==================================================================
        // entry points
        // ==================================================================

        /// <summary>Starts a battle. Pass a trainer name for a duel, or null for a wild encounter.</summary>
        public void Begin(List<EggInstance> team, string opponentName)
        {
            foeTeam.Clear();
            foeTeam.AddRange(team);
            foeIndex = 0;
            trainerName = opponentName;
            battleOver = false;
            outcome = BattleOutcome.Won;

            activeIndex = 0;
            for (int i = 0; i < State.Party.Count; i++)
            {
                if (!State.Party[i].IsFainted) { activeIndex = i; break; }
            }

            for (int i = 0; i < State.Party.Count; i++) State.Party[i].ClearStages();
            for (int i = 0; i < foeTeam.Count; i++)
            {
                foeTeam[i].ClearStages();
                State.Seen.Add(foeTeam[i].Species.Id);
            }

            canvas.gameObject.SetActive(true);
            HideAllMenus();
            RefreshCards(true);
            routine = StartCoroutine(Run());
        }

        /// <summary>Everyone who spent time out in front this fight.</summary>
        readonly HashSet<int> foughtThisBattle = new HashSet<int>();

        void Finish(BattleOutcome result)
        {
            // Credited on the way out rather than per turn, so a fight is a fight however many
            // rounds it ran - and everybody who was sent out gets it, not only whoever happened
            // to be standing there at the end.
            foughtThisBattle.Add(activeIndex);
            foreach (int i in foughtThisBattle)
                if (i >= 0 && i < State.Party.Count) State.Party[i].RecordFight();
            foughtThisBattle.Clear();

            // Conditions last the fight and no longer. Clearing here rather than on the way
            // out means a caught egg and a fainted one are both handed back clean.
            for (int i = 0; i < State.Party.Count; i++) State.Party[i].ClearStatus();
            if (Foe != null) Foe.ClearStatus();
            battleOver = true;
            outcome = result;
        }

        // ==================================================================
        // the battle itself
        // ==================================================================

        IEnumerator Run()
        {
            if (IsTrainer)
            {
                yield return Say(BattleLog.TrainerSendsOut(trainerName, Foe.Name, Foe.Level));
            }
            else if (Foe.Elder)
            {
                yield return Say(BattleLog.ElderAppears(Foe.Species.Name, Foe.Level));
                yield return Say(UiCopy.ElderWarning);
            }
            else
            {
                yield return Say(BattleLog.WildAppears(Foe.Name, Foe.Level));
            }

            yield return Say(BattleLog.GoOut(Mine.Name));

            rounds = 0;
            foeWasElder = Foe.Elder;
            while (!battleOver)
            {
                rounds++;
                if (Mine.IsFainted)
                {
                    if (!AnyAliveInParty())
                    {
                        Finish(BattleOutcome.Lost);
                        break;
                    }
                    yield return ForcedSwap();
                    if (battleOver) break;
                }

                // Lune explains salves properly, on Shimmerfen, in the second sector. A player
                // carries four of them from the first minute and nobody says a word about them
                // until then - and the balance run puts them at seventeen points on the Amy
                // fight. So the game says it once, the first time an egg is actually hurt
                // enough for it to matter, which is the same way the Elder line works.
                if (!dir.Story.HasFlag("learned_salve") && dir.State.Salves > 0 &&
                    Mine.HPFraction < SalveHintFraction)
                {
                    dir.Story.SetFlag("learned_salve");
                    yield return Say(BattleLog.SalveOffered(Mine.Name));
                }

                yield return ChooseTurn();
                if (battleOver) break;

                yield return ResolveTurn();
                yield return TickRoundEnd();
            }

            yield return Epilogue();

            canvas.gameObject.SetActive(false);
            routine = null;
            dir.OnBattleFinished(outcome);
        }

        IEnumerator ChooseTurn()
        {
            while (true)
            {
                messageText.text = "What will " + Mine.Name + " do?";
                RefreshActionButtons();
                ShowMenu(actionPanel);
                menuHint.text = UiCopy.BattleFooter;
                yield return WaitChoice(actionButtons, 2, false);
                HideAllMenus();
                if (pendingChoice < 0) continue;

                switch ((PlayerAction)pendingChoice)
                {
                    case PlayerAction.Fight:
                        if (!Mine.HasUsableMove())
                        {
                            // Every move is out of PP; fall through to Flail rather than trapping the player.
                            chosenAction = PlayerAction.Fight;
                            chosenParam = -1;
                            yield break;
                        }
                        RefreshMoveButtons();
                        ShowMenu(movePanel);
                        // The message box is taken over by the highlighted move's description.
                        menuHint.text = "arrows browse · Enter to use · Esc to go back";
                        yield return WaitChoice(moveButtons, 2, true);
                        HideAllMenus();
                        if (pendingChoice == -2) continue;
                        chosenAction = PlayerAction.Fight;
                        chosenParam = pendingChoice;
                        yield break;

                    case PlayerAction.Carton:
                        if (IsTrainer)
                        {
                            yield return Say(BattleLog.NoStealing(trainerName));
                            continue;
                        }
                        if (State.Cartons <= 0)
                        {
                            yield return Say(UiCopy.OutOfCartons);
                            continue;
                        }
                        chosenAction = PlayerAction.Carton;
                        yield break;

                    case PlayerAction.Salve:
                        if (State.Salves <= 0)
                        {
                            yield return Say(UiCopy.OutOfSalves);
                            continue;
                        }
                        if (Mine.CurrentHP >= Mine.MaxHP)
                        {
                            yield return Say(BattleLog.SalveNotNeeded(Mine.Name));
                            continue;
                        }
                        chosenAction = PlayerAction.Salve;
                        yield break;

                    case PlayerAction.Swap:
                        if (CountAliveInParty() <= 1)
                        {
                            yield return Say(UiCopy.NobodyLeft);
                            continue;
                        }
                        RefreshPartyButtons();
                        UIKit.SetActive(partyPanel, true);
                        messageText.text = "Send out which egg?";
                        yield return WaitChoice(partyButtons, 1, true);
                        UIKit.SetActive(partyPanel, false);
                        if (pendingChoice == -2) continue;
                        chosenAction = PlayerAction.Swap;
                        chosenParam = pendingChoice;
                        yield break;

                    case PlayerAction.Run:
                        if (IsTrainer)
                        {
                            yield return Say(BattleLog.NoRunning(trainerName));
                            continue;
                        }
                        chosenAction = PlayerAction.Run;
                        yield break;
                }
            }
        }

        IEnumerator ResolveTurn()
        {
            switch (chosenAction)
            {
                case PlayerAction.Swap:
                    {
                        var incoming = State.Party[chosenParam];
                        Mine.ClearStages();
                        yield return Say(BattleLog.ComeBack(Mine.Name));
                        activeIndex = chosenParam;
                        foughtThisBattle.Add(activeIndex);
                        RefreshCards(true);
                        // Whatever came out says so. The record page speaks when you highlight a
                        // species and the wild one speaks when it arrives; the egg you send in
                        // yourself was the one thing that came on in silence.
                        dir.Audio.PlayCry(incoming.Species, AudioDirector.CrySentOut);
                        yield return Say(BattleLog.GoOut(incoming.Name));
                        yield return FoeTurn();
                        yield break;
                    }

                case PlayerAction.Salve:
                    yield return UseSalve();
                    yield return FoeTurn();
                    yield break;

                case PlayerAction.Carton:
                    yield return ThrowCarton();
                    if (battleOver) yield break;
                    yield return FoeTurn();
                    yield break;

                case PlayerAction.Run:
                    {
                        if (EggRandom.Value < BattleCalc.FleeChance(Mine, Foe))
                        {
                            yield return Say(UiCopy.FledSafely);
                            Finish(BattleOutcome.Fled);
                            yield break;
                        }
                        yield return Say(UiCopy.CouldNotFlee);
                        yield return FoeTurn();
                        yield break;
                    }

                default:
                    {
                        MoveSlot mySlot = chosenParam >= 0 && chosenParam < Mine.Moves.Count ? Mine.Moves[chosenParam] : null;
                        bool meFirst = BattleCalc.MoverGoesFirst(Mine, Foe);

                        if (meFirst)
                        {
                            yield return PlayerTurn(mySlot);
                            if (battleOver || Mine.IsFainted) yield break;
                            yield return FoeTurn();
                        }
                        else
                        {
                            yield return FoeTurn();
                            if (battleOver || Mine.IsFainted) yield break;
                            yield return PlayerTurn(mySlot);
                        }
                        yield break;
                    }
            }
        }

        IEnumerator PlayerTurn(MoveSlot slot)
        {
            yield return UseMove(Mine, Foe, slot, true);
            if (battleOver) yield break;
            if (Foe.IsFainted) yield return OnFoeFainted();
            else if (Mine.IsFainted) yield return OnMineFainted();   // recoil can knock you out
        }

        IEnumerator FoeTurn()
        {
            if (Foe.IsFainted || battleOver) yield break;
            var slot = PickAiMove();
            yield return UseMove(Foe, Mine, slot, false);
            if (Mine.IsFainted) yield return OnMineFainted();
        }

        MoveSlot PickAiMove()
        {
            MoveSlot best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < Foe.Moves.Count; i++)
            {
                var s = Foe.Moves[i];
                if (!s.Usable) continue;
                float score = BattleCalc.AiScore(Foe, Mine, s.Move) * EggRandom.Range(0.85f, 1.15f);
                if (score > bestScore) { bestScore = score; best = s; }
            }
            return best;
        }

        /// <summary>How often a dazed egg loses its turn outright.</summary>
        const float DazeSkipChance = 0.25f;

        IEnumerator UseMove(EggInstance user, EggInstance target, MoveSlot slot, bool userIsPlayer)
        {
            // Checked before the move is chosen off the slot, so a lost turn costs no PP.
            if (user.Status == EggStatus.Dazed && EggRandom.Value < DazeSkipChance)
            {
                yield return Say(BattleLog.TooDazed(user.Name));
                yield break;
            }

            MoveDef move;
            if (slot == null || !slot.Usable)
            {
                move = Flail;
                yield return Say(BattleLog.OutOfMoves(user.Name));
            }
            else
            {
                move = slot.Move;
                slot.PP--;
                yield return Say(BattleLog.Used(user.Name, move.Name));
            }

            if (!BattleCalc.Hits(move))
            {
                yield return Say(UiCopy.Missed);
                yield break;
            }

            if (move.IsStatus)
            {
                yield return ApplyStatusEffect(user, target, move);
                RefreshCards(false);
                yield break;
            }

            int hits = move.Effect == MoveEffect.MultiHit2 ? 2 : 1;
            int totalDealt = 0;
            float typeMult = 1f;

            for (int h = 0; h < hits && !target.IsFainted; h++)
            {
                bool crit;
                int raw = BattleCalc.Damage(user, target, move, out typeMult, out crit);
                bool sturdySave = target.Trait == EggTrait.Sturdy && target.CurrentHP == target.MaxHP && raw >= target.CurrentHP;
                int dealt = target.TakeHit(raw);
                totalDealt += dealt;

                dir.Audio.PlayHit(typeMult, crit);
                Image victimImage = userIsPlayer ? foeEggImage : myEggImage;
                SpawnDamageNumber(victimImage, dealt, typeMult, crit);
                Shake(crit ? 26f : TypeChart.IsStrong(typeMult) ? 18f : TypeChart.IsWeak(typeMult) ? 5f : 11f);
                yield return AnimateHit(victimImage,
                                        userIsPlayer ? foeHpBar : myHpBar,
                                        target);
                if (crit) yield return Say(UiCopy.Critical);
                if (sturdySave) yield return Say(BattleLog.HeldOn(target.Name));

                // Static punishes whoever threw the punch.
                if (target.Trait == EggTrait.Static && dealt > 0 && !user.IsFainted)
                {
                    int jolt = Mathf.Max(1, Mathf.RoundToInt(dealt * BattleCalc.StaticShare));
                    user.TakeDamage(jolt);
                    SpawnDamageNumber(userIsPlayer ? myEggImage : foeEggImage, jolt, 1f, false);
                    RefreshCards(false);
                    yield return Say(BattleLog.Jolted(user.Name, jolt));
                }
            }

            if (hits > 1 && !target.IsFainted) yield return Say(BattleLog.HitTimes(hits));

            string line = TypeChart.EffectivenessLine(typeMult);
            if (line != null) yield return Say(line);

            switch (move.Effect)
            {
                case MoveEffect.Lifesteal50:
                    {
                        int healed = Mathf.Max(1, Mathf.RoundToInt(totalDealt * MoveDef.LifestealShare));
                        user.Heal(healed);
                        SpawnHealNumber(userIsPlayer ? myEggImage : foeEggImage, healed);
                        RefreshCards(false);
                        yield return Say(BattleLog.Drained(user.Name, healed));
                        break;
                    }
                case MoveEffect.Recoil25:
                    {
                        int recoil = Mathf.Max(1, Mathf.RoundToInt(totalDealt * MoveDef.RecoilShare));
                        user.TakeDamage(recoil);
                        SpawnDamageNumber(userIsPlayer ? myEggImage : foeEggImage, recoil, 1f, false);
                        RefreshCards(false);
                        yield return Say(BattleLog.Recoiled(user.Name, recoil));
                        break;
                    }
                case MoveEffect.SpdDownFoe:
                    yield return LowerFoeSpeed(target);
                    break;

                case MoveEffect.Scorch:
                case MoveEffect.Chill:
                case MoveEffect.Daze:
                    yield return Inflict(target, BattleCalc.RiderOf(move.Effect));
                    break;
            }

            RefreshCards(false);

            if (!userIsPlayer && user.IsFainted) yield return OnFoeFainted();
        }

        IEnumerator ApplyStatusEffect(EggInstance user, EggInstance target, MoveDef move)
        {
            switch (move.Effect)
            {
                case MoveEffect.Heal50:
                    {
                        int before = user.CurrentHP;
                        user.Heal(Mathf.RoundToInt(user.MaxHP * MoveDef.SelfHealShare));
                        SpawnHealNumber(ReferenceEquals(user, Mine) ? myEggImage : foeEggImage, user.CurrentHP - before);
                        yield return Say(BattleLog.Mended(user.Name, user.CurrentHP - before));
                        break;
                    }
                case MoveEffect.AtkUp:
                    user.AtkStage = Mathf.Min(6, user.AtkStage + 1);
                    yield return Say(BattleLog.AttackRose(user.Name));
                    break;
                case MoveEffect.DefUp:
                    user.DefStage = Mathf.Min(6, user.DefStage + 1);
                    yield return Say(BattleLog.DefenceRose(user.Name));
                    break;
                case MoveEffect.SpdUp:
                    user.SpdStage = Mathf.Min(6, user.SpdStage + 1);
                    yield return Say(BattleLog.SpeedRose(user.Name));
                    break;
                case MoveEffect.SpdDownFoe:
                    yield return LowerFoeSpeed(target);
                    break;
                default:
                    yield return Say(UiCopy.NoEffect);
                    break;
            }
        }

        static string StatusVerb(EggStatus s)
        {
            switch (s)
            {
                case EggStatus.Scorched: return " is scorched, and will keep burning!";
                case EggStatus.Chilled:  return " is chilled, and has slowed right down!";
                case EggStatus.Dazed:    return " is dazed, and may lose its footing!";
            }
            return "";
        }

        /// <summary>Lands a lingering condition, or says why it did not.</summary>
        IEnumerator Inflict(EggInstance target, EggStatus status)
        {
            if (target.IsFainted || status == EggStatus.None) yield break;

            if (target.Status == status)
            {
                yield return Say(BattleLog.AlreadyHas(target.Name, status));
                yield break;
            }
            if (!target.CanCatch(status))
            {
                // Either it is already carrying something else, or its own element shrugs this off.
                if (target.Status == EggStatus.None)
                    yield return Say(BattleLog.ImmuneTo(target.Type));
                yield break;
            }

            target.Afflict(status);
            dir.Audio.Play(Sfx.Debuff);
            RefreshCards(false);
            yield return Say(target.Name + StatusVerb(status));
        }

        IEnumerator LowerFoeSpeed(EggInstance target)
        {
            int stage = target.SpdStage;
            if (target.TryLowerStage(ref stage))
            {
                target.SpdStage = stage;
                yield return Say(BattleLog.SpeedFell(target.Name));
            }
            else
            {
                yield return Say(BattleLog.TooHardheaded(target.Name));
            }
        }

        /// <summary>End-of-round upkeep: Warm Yolk mends a little.</summary>
        IEnumerator TickRoundEnd()
        {
            if (battleOver) yield break;

            int mine = Mine.TickRegen();
            if (mine > 0)
            {
                RefreshCards(false);
                yield return Say(BattleLog.MendedShort(Mine.Name, mine));
            }

            int theirs = Foe.TickRegen();
            if (theirs > 0)
            {
                RefreshCards(false);
                yield return Say(BattleLog.MendedShort(Foe.Name, theirs));
            }

            // Burn ticks after regeneration, so Warm Yolk offsets it rather than racing it.
            foreach (var burning in new[] { Mine, Foe })
            {
                int tick = burning.StatusTickDamage();
                if (tick <= 0 || burning.IsFainted) continue;
                burning.TakeDamage(tick);
                SpawnDamageNumber(burning == Mine ? myEggImage : foeEggImage, tick, 1f, false);
                yield return AnimateHit(burning == Mine ? myEggImage : foeEggImage,
                                        burning == Mine ? myHpBar : foeHpBar, burning);
                yield return Say(BattleLog.Burned(burning.Name, tick));
                if (burning.IsFainted) break;
            }

            // Then count the conditions down, so the round it lands is a full round of it.
            foreach (var egg in new[] { Mine, Foe })
            {
                var had = egg.Status;
                if (egg.TickStatus())
                {
                    RefreshCards(false);
                    yield return Say(BattleLog.ShookOff(egg.Name, had));
                }
            }
        }

        /// <summary>Mends the active egg for a little over half its bulk, and costs the turn.</summary>
        IEnumerator UseSalve()
        {
            State.Salves--;
            var egg = Mine;
            int before = egg.CurrentHP;
            egg.Heal(Mathf.Max(1, Mathf.RoundToInt(egg.MaxHP * SalveFraction)));
            int healed = egg.CurrentHP - before;
            dir.Audio.Play(Sfx.Heal);
            yield return Say(BattleLog.UsedSalve(State.Salves));
            SpawnHealNumber(myEggImage, healed);
            yield return AnimateHeal(myHpBar, myEggImage, egg);
            yield return Say(BattleLog.Recovered(egg.Name, healed));
            State.RaiseChanged();
        }

        IEnumerator ThrowCarton()
        {
            State.Cartons--;
            State.RaiseChanged();
            dir.Audio.Play(Sfx.CartonThrow);
            yield return Say(BattleLog.ThrewCarton(State.Cartons));

            int shakes;
            bool caught = BattleCalc.RollCatch(Foe, out shakes);

            for (int i = 0; i < Mathf.Max(1, shakes); i++)
            {
                dir.Audio.Play(Sfx.CartonWobble);
                yield return WobbleCarton();
                yield return Say(". . .", 0.35f);
            }

            if (caught)
            {
                dir.Audio.Play(Sfx.CatchSuccess);
                var prize = Foe;
                // The one you just caught says something. It is the only moment in the game
                // where an egg becomes yours, and the carton latching was the whole of it.
                StartCoroutine(CryAfter(prize.Species, 0.30f));
                // Recorded before Collect, which is what adds it to the record.
                LastCatchWasNewSpecies = !State.Caught.Contains(prize.Species.Id);
                LastCatchWasElder = prize.Elder;
                bool intoParty = State.Collect(prize);
                LastCatchName = prize.Name;
                LastCatchJoinedParty = intoParty;
                yield return Say("Gotcha! " + prize.Name + " was collected!");

                // Offer a nickname, but never force the player through a text prompt.
                // Watch for N across the whole message rather than sampling once at the end.
                bool wantsName = false;
                messageText.text = "Press <b>N</b> to name it, or Space to carry on.";
                menuHint.text = "";
                float waited = 0f;
                while (waited < 2.6f)
                {
                    waited += Time.deltaTime;
                    if (EggInput.NKeyPressed) { wantsName = true; break; }
                    if (waited > 0.2f && (EggInput.ConfirmPressed || EggInput.InteractPressed)) break;
                    yield return null;
                }

                if (wantsName)
                {
                    bool naming = true;
                    dir.NameEntry.Open(prize, chosen =>
                    {
                        if (!string.IsNullOrEmpty(chosen)) prize.Nickname = chosen;
                        naming = false;
                    });
                    yield return new WaitUntil(() => !naming);
                    if (!string.IsNullOrEmpty(prize.Nickname))
                        yield return Say(prize.Species.Name + " will answer to " + prize.Nickname + " now.");
                    State.RaiseChanged();
                }

                yield return Say(intoParty
                    ? prize.Name + " joined your party."
                    : prize.Name + " was sent to the nest back home.");
                Finish(BattleOutcome.Caught);
            }
            else
            {
                dir.Audio.Play(Sfx.CatchFail);
                yield return Say(shakes >= 2 ? "So close! It broke free." : "It burst straight out!");
            }
        }

        IEnumerator OnFoeFainted()
        {
            dir.Audio.Play(Sfx.Faint);
            yield return Say(Foe.Name + " cracked and gave up!");

            int reward = Foe.XpRewardFor();
            var log = new List<string>();
            var evolved = new List<EggInstance>();

            // Worked out before the award, because AwardXp mutates the egg on the spot and the
            // path is gone the moment it does.
            var path = XpFill.Path(Mine.Level, Mine.Xp, EggInstance.MaxLevel, reward);
            State.AwardXp(Mine, reward, evolved, log);

            yield return Say(Mine.Name + " gained " + reward + " XP.");
            yield return AnimateXp(path);
            for (int i = 0; i < log.Count; i++) yield return Say(log[i]);

            if (evolved.Contains(Mine))
            {
                dir.Audio.Play(Sfx.Evolve);
                RefreshCards(true);
                yield return AnimateEvolution(myEggImage);
            }

            // Everyone else who can still stand shares a little.
            int share = Mathf.Max(1, reward * GameState.BenchXpPercent / 100);
            for (int i = 0; i < State.Party.Count; i++)
            {
                if (i == activeIndex || State.Party[i].IsFainted) continue;
                var extra = new List<string>();
                var benchEvolved = new List<EggInstance>();
                State.AwardXp(State.Party[i], share, benchEvolved, extra);
                if (benchEvolved.Count > 0) dir.Audio.Play(Sfx.Evolve);
                for (int j = 0; j < extra.Count; j++) yield return Say(extra[j]);
            }

            RefreshCards(false);
            State.RaiseChanged();

            if (foeIndex + 1 < foeTeam.Count)
            {
                foeIndex++;
                yield return Say((IsTrainer ? trainerName : "The wild nest") + " sends out " + Foe.Name + "!");
                RefreshCards(true);
            }
            else
            {
                Finish(BattleOutcome.Won);
            }
        }

        IEnumerator OnMineFainted()
        {
            dir.Audio.Play(Sfx.Faint);
            yield return Say(Mine.Name + " is out cold.");
            Mine.ClearStages();
            if (!AnyAliveInParty()) Finish(BattleOutcome.Lost);
        }

        IEnumerator ForcedSwap()
        {
            RefreshPartyButtons();
            UIKit.SetActive(partyPanel, true);
            messageText.text = "Send out which egg?";
            yield return WaitChoice(partyButtons, 1, false);
            UIKit.SetActive(partyPanel, false);
            if (pendingChoice >= 0) { activeIndex = pendingChoice; foughtThisBattle.Add(activeIndex); }
            RefreshCards(true);
            dir.Audio.PlayCry(Mine.Species, AudioDirector.CrySentOut);
            yield return Say("Go, " + Mine.Name + "!");
        }

        IEnumerator Epilogue()
        {
            switch (outcome)
            {
                case BattleOutcome.Won:
                    yield return Say(IsTrainer ? trainerName + " is out of eggs." : WildVictoryLine());
                    break;
                case BattleOutcome.Lost:
                    yield return Say(UiCopy.AllOutCold);
                    break;
            }
        }

        /// <summary>
        /// What the game says after a wild win. "You won the scrap." was the single most
        /// repeated line in the whole game and it said the same thing after a one-hit knock and
        /// after clawing back from one HP. The fight already knows which it was.
        /// </summary>
        string WildVictoryLine()
        {
            var mine = Mine;
            if (foeWasElder) return "An Elder, no less. " + mine.Name + " stands over it.";
            if (mine.CurrentHP >= mine.MaxHP) return "Not a scratch on " + mine.Name + ".";
            if (rounds <= 1) return "One hit. " + mine.Name + " barely looked up.";
            if (mine.HPFraction < 0.2f) return "That was close. " + mine.Name + " is still standing, just.";
            if (rounds >= 8) return "A long one. Both of them are breathing hard.";
            return "You won the scrap.";
        }

        // ==================================================================
        // presentation
        // ==================================================================

        IEnumerator Say(string text, float hold = 1.0f)
        {
            messageText.text = text;
            menuHint.text = "press Space to continue";

            // The battle talks a great deal, and how long each line sits there is the same
            // preference as how fast dialogue reveals. At "instant" the lines still appear, they
            // just do not wait - a player who has read "It's super effective!" two hundred times
            // should not be made to read it again.
            float scale = State != null ? State.RevealScale : 1f;
            hold = scale <= 0f ? 0f : hold / scale;

            float t = 0f;
            while (t < hold)
            {
                t += Time.deltaTime;
                if (t > 0.18f && (EggInput.ConfirmPressed || EggInput.InteractPressed)) break;
                yield return null;
            }
            menuHint.text = "";
        }

        /// <summary>A salve mends a little over half the egg's bulk.</summary>
        const float SalveFraction = 0.55f;

        /// <summary>The healing counterpart to AnimateHit: bar climbs, egg flashes green, no shake.</summary>
        IEnumerator AnimateHeal(BarWidget bar, Image portrait, EggInstance target)
        {
            float from = bar.FillRect.anchorMax.x;
            float to = target.HPFraction;
            float t = 0f;
            while (t < 0.40f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.40f);
                float f = Mathf.Lerp(from, to, k);
                bar.SetFraction(f);
                bar.SetFillColor(UIKit.HealthColor(f));
                portrait.color = Color.Lerp(new Color(0.62f, 1f, 0.68f, 1f), Color.white, k);
                yield return null;
            }
            portrait.color = Color.white;
            bar.SetFraction(to);
            bar.SetFillColor(UIKit.HealthColor(to));
            RefreshCards(false);
        }

        /// <summary>
        /// Runs the XP bar along its path, sounding each level as the bar completes rather than
        /// once at the end of all of them. The bar used to snap - and when the award crossed a
        /// level it snapped backwards, so the one moment worth watching never happened.
        /// </summary>
        // Where the two eggs sit when nothing is happening to them. Captured once, because an
        // animation that reads the current position as "home" and then restores it will leave
        // the egg wherever the idle happened to have it that frame.
        Vector2 mineHome, foeHome;
        bool homesTaken;

        /// <summary>Suppresses the idle while something else is moving an egg on purpose.</summary>
        int animating;

        /// <summary>
        /// How far an egg drifts as it sits there, and how fast. Small: this is breathing, not
        /// bouncing, and it plays for the whole of every fight.
        /// </summary>
        public const float IdleRise = 7f, IdleRate = 1.35f;

        /// <summary>
        /// Where the two eggs and their two cards sit, in screen pixels on the 1920x1080 canvas.
        ///
        /// Public because the check measures the real placement, and because the render drew the
        /// eggs from two colour pairs typed against two positions — which had drifted, so the
        /// picture showed the player's green egg in the foe's slot. Reading that screenshot the
        /// layout looked broken; it was not, the picture was. A transcription raises a false
        /// alarm as readily as it gives false comfort.
        ///
        /// Each egg sits diagonally opposite its own card, which is what stops either covering
        /// the other on a screen with four things on it.
        /// </summary>
        public static readonly Rect FoeEggRect  = new Rect(1920f - 430f - 150f, 1080f - 330f - 150f, 300f, 300f);
        public static readonly Rect FoeCardRect = new Rect(70f, 1080f - 70f - 150f, 660f, 150f);
        public static readonly Rect MyEggRect   = new Rect(440f - 180f, 545f - 180f, 360f, 360f);
        public static readonly Rect MyCardRect  = new Rect(1920f - 70f - 700f, 540f - 110f - 210f, 700f, 210f);

        /// <summary>
        /// A pair of phases far enough apart that two eggs never move as one object. The surface
        /// learned this with its warming ring - six eggs rising in lockstep read as one lid
        /// lifting rather than six creatures.
        /// </summary>
        public const float MinePhase = 0f, FoePhase = 2.1f;

        /// <summary>
        /// The battle screen's eggs sat perfectly still for the whole of every fight, except for
        /// the third of a second one of them was being hit. The surface has bobbed its wildlife
        /// since it was written; the screen a player spends most of the game looking at had
        /// nothing.
        /// </summary>
        void TickIdle()
        {
            if (myEggImage == null || foeEggImage == null) return;
            if (!homesTaken)
            {
                mineHome = myEggImage.rectTransform.anchoredPosition;
                foeHome = foeEggImage.rectTransform.anchoredPosition;
                homesTaken = true;
            }
            if (animating > 0) return;

            // Off means off, the same as the hit shake. Someone who turned screen motion down
            // did not mean "except for this".
            if (State != null && !State.ScreenMotion)
            {
                myEggImage.rectTransform.anchoredPosition = mineHome;
                foeEggImage.rectTransform.anchoredPosition = foeHome;
                return;
            }

            float t = Time.time * IdleRate;
            // A fainted egg does not breathe.
            float mineAmp = Mine != null && !Mine.IsFainted ? IdleRise : 0f;
            float foeAmp = Foe != null && !Foe.IsFainted ? IdleRise : 0f;

            myEggImage.rectTransform.anchoredPosition =
                mineHome + new Vector2(0f, Mathf.Sin(t + MinePhase) * mineAmp);
            foeEggImage.rectTransform.anchoredPosition =
                foeHome + new Vector2(0f, Mathf.Sin(t + FoePhase) * foeAmp);
        }

        IEnumerator CryAfter(SpeciesDef sp, float delay)
        {
            yield return new WaitForSeconds(delay);
            dir.Audio.PlayCry(sp, AudioDirector.CryCaught);
        }

        IEnumerator AnimateXp(List<XpFill.Step> path)
        {
            if (path == null || path.Count == 0) yield break;

            float each = XpFill.SecondsFor(path.Count);
            foreach (var step in path)
            {
                if (Mathf.Approximately(step.From, step.To))
                {
                    myXpBar.SetFraction(step.To);
                    continue;
                }

                float t = 0f;
                while (t < each)
                {
                    t += Time.deltaTime;
                    myXpBar.SetFraction(Mathf.Lerp(step.From, step.To, Mathf.Clamp01(t / each)));
                    yield return null;
                }
                myXpBar.SetFraction(step.To);

                // The note lands on the bar filling, not two lines of text later.
                if (step.Levels)
                {
                    dir.Audio.Play(Sfx.LevelUp);
                    myXpBar.SetFraction(XpFill.Rests(step));
                    yield return null;
                }
            }
        }

        /// <summary>
        /// The HP number partway through a bar's slide. Rounds toward the value being left, so a
        /// hit that takes an egg to zero never shows 0 until the bar is actually empty - and the
        /// last frame is the real number rather than a rounding of it.
        /// </summary>
        public static int HpShown(float from, float to, float k, int maxHp)
        {
            if (k >= 1f) return Mathf.RoundToInt(to * maxHp);
            float f = Mathf.Lerp(from, to, k);
            int hp = to < from ? Mathf.CeilToInt(f * maxHp) : Mathf.FloorToInt(f * maxHp);
            return Mathf.Clamp(hp, 0, maxHp);
        }

        IEnumerator AnimateHit(Image victim, BarWidget bar, EggInstance target)
        {
            float from = bar.FillRect.anchorMax.x;
            float to = target.HPFraction;
            animating++;
            Vector2 home = victim == myEggImage ? mineHome : foeHome;

            float t = 0f;
            while (t < 0.34f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.34f);
                bar.SetFraction(Mathf.Lerp(from, to, k));
                bar.SetFillColor(UIKit.HealthColor(Mathf.Lerp(from, to, k)));

                // The number counts down with the bar. TakeDamage lands before this runs and
                // RefreshCards only ran at the end, so for a third of a second the bar showed a
                // third full while the text beside it still read 120/135 - the two things that
                // say the same thing, saying different things, on every hit in the game.
                if (target == Mine)
                    myHpText.text = UiCopy.Health(HpShown(from, to, k, target.MaxHP), target.MaxHP);

                float shake = State.ScreenMotion ? (1f - k) * 16f : 0f;
                victim.rectTransform.anchoredPosition = home + new Vector2(Mathf.Sin(t * 60f) * shake, 0f);
                victim.color = Color.Lerp(new Color(1f, 0.55f, 0.55f, 1f), Color.white, k);
                yield return null;
            }

            victim.rectTransform.anchoredPosition = home;
            victim.color = Color.white;
            animating--;
            bar.SetFraction(to);
            bar.SetFillColor(UIKit.HealthColor(to));
            RefreshCards(false);   // lands on the true value, which HpShown is pinned to at k=1

            if (target.IsFainted)
            {
                float f = 0f;
                while (f < 0.3f)
                {
                    f += Time.deltaTime;
                    victim.color = new Color(1f, 1f, 1f, 1f - f / 0.3f);
                    yield return null;
                }
                victim.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        // ---------- hit feedback ----------

        public void Shake(float strength)
        {
            if (!State.ScreenMotion) return;
            shakeStrength = Mathf.Max(shakeStrength, strength);
        }

        void TickShake()
        {
            if (shakeRoot == null) return;
            if (shakeStrength <= 0.01f)
            {
                if (shakeRoot.anchoredPosition != Vector2.zero) shakeRoot.anchoredPosition = Vector2.zero;
                return;
            }
            shakeStrength = Mathf.Lerp(shakeStrength, 0f, 1f - Mathf.Exp(-11f * Time.deltaTime));
            shakeRoot.anchoredPosition = new Vector2(
                (Random.value * 2f - 1f) * shakeStrength,
                (Random.value * 2f - 1f) * shakeStrength * 0.7f);
        }

        /// <summary>Floating damage figure that rises off the struck egg and fades.</summary>
        void SpawnDamageNumber(Image target, int amount, float typeMultiplier, bool critical)
        {
            Color color = critical ? new Color32(0xFF, 0xE0, 0x5C, 0xFF)
                        : TypeChart.IsStrong(typeMultiplier) ? new Color32(0xFF, 0x8A, 0x3D, 0xFF)
                        : TypeChart.IsWeak(typeMultiplier) ? new Color32(0x9A, 0xA4, 0xB6, 0xFF)
                        : Color.white;

            int size = critical ? 62 : TypeChart.IsStrong(typeMultiplier) ? 54 : 44;
            SpawnNumber(target, (critical ? "!" : "") + amount, color, size);
        }

        /// <summary>
        /// A number for health going the other way.
        ///
        /// Damage has floated one since the fight screen was written. Every other change to an
        /// egg's health did not: a salve, a drain, the recoil off your own move, the tick off a
        /// burn, the Static trait jolting whoever threw the punch. Six ways to move a health bar
        /// and one of them said by how much. The bar and the line of text both moved, so nothing
        /// was hidden - it just made the one that mattered least the only one that felt like it
        /// landed.
        /// </summary>
        void SpawnHealNumber(Image target, int amount)
        {
            if (amount <= 0) return;
            SpawnNumber(target, "+" + amount, new Color32(0x5F, 0xD0, 0x68, 0xFF), 44);
        }

        void SpawnNumber(Image target, string text, Color color, int size)
        {
            var label = UIKit.Label(shakeRoot, "Number", text, size, color,
                                    TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(label.rectTransform, target.rectTransform.anchorMin, new Vector2(0.5f, 0.5f),
                        target.rectTransform.anchoredPosition + new Vector2(Random.Range(-40f, 40f), 40f),
                        new Vector2(300f, 90f));
            StartCoroutine(FloatDamageNumber(label));
        }

        IEnumerator FloatDamageNumber(Text label)
        {
            Vector2 start = label.rectTransform.anchoredPosition;
            float drift = Random.Range(-26f, 26f);
            float t = 0f;
            while (t < 0.85f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.85f);
                label.rectTransform.anchoredPosition = start + new Vector2(drift * k, 120f * Mathf.Sqrt(k));
                // Pop out, then settle, then fade.
                float pop = k < 0.16f ? Mathf.Lerp(0.55f, 1.15f, k / 0.16f) : Mathf.Lerp(1.15f, 1f, (k - 0.16f) / 0.84f);
                label.rectTransform.localScale = Vector3.one * pop;
                var c = label.color;
                c.a = k < 0.62f ? 1f : 1f - (k - 0.62f) / 0.38f;
                label.color = c;
                yield return null;
            }
            if (label != null) Destroy(label.gameObject);
        }

        /// <summary>A bright flare that swells and settles, covering the species swap.</summary>
        IEnumerator AnimateEvolution(Image target)
        {
            Vector2 size = target.rectTransform.sizeDelta;
            float t = 0f;
            while (t < 1.5f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 1.5f);
                float pulse = 1f + Mathf.Sin(t * 22f) * 0.10f * (1f - k);
                float flare = Mathf.Sin(k * Mathf.PI);
                target.rectTransform.sizeDelta = size * pulse * Mathf.Lerp(1f, 1.12f, flare);
                target.color = new Color(1f, Mathf.Lerp(1f, 0.85f, flare), Mathf.Lerp(1f, 0.6f, flare), 1f);
                yield return null;
            }
            target.rectTransform.sizeDelta = size;
            target.color = Color.white;
        }

        IEnumerator WobbleCarton()
        {
            float t = 0f;
            animating++;
            Vector2 home = foeHome;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                foeEggImage.rectTransform.anchoredPosition = home + new Vector2(Mathf.Sin(t * 22f) * 14f, 0f);
                yield return null;
            }
            foeEggImage.rectTransform.anchoredPosition = home;
            animating--;
        }

        /// <summary>Arrows for any raised or lowered stat, so buffs are not invisible.</summary>
        /// <summary>The condition chip that sits on a combatant's card, or nothing.</summary>
        static string StatusTag(EggInstance egg)
        {
            switch (egg.Status)
            {
                case EggStatus.Scorched: return "  <color=#E5734A>SCORCHED</color>";
                case EggStatus.Chilled:  return "  <color=#8FE3F2>CHILLED</color>";
                case EggStatus.Dazed:    return "  <color=#FFC24D>DAZED</color>";
            }
            return "";
        }

        static string Stages(EggInstance egg)
        {
            var sb = new System.Text.StringBuilder();
            AppendStage(sb, "ATK", egg.AtkStage);
            AppendStage(sb, "DEF", egg.DefStage);
            AppendStage(sb, "SPD", egg.SpdStage);
            return sb.Length == 0 ? "" : "  " + sb;
        }

        static void AppendStage(System.Text.StringBuilder sb, string label, int stage)
        {
            if (stage == 0) return;
            if (sb.Length > 0) sb.Append(' ');
            string arrows = new string(stage > 0 ? '▲' : '▼', Mathf.Min(3, Mathf.Abs(stage)));
            sb.Append("<size=18><color=").Append(stage > 0 ? "#5FD068" : "#E55555").Append('>')
              .Append(label).Append(arrows).Append("</color></size>");
        }

        /// <summary>The name and level as drawn on a battle plate.</summary>
        public static string PlateName(EggInstance egg) =>
            egg.Name + "  <size=22><color=#A8B2C4>Lv " + egg.Level + "</color></size>";

        /// <summary>
        /// The line under the name: trait, any status, and any stat stages. The stages carry
        /// arrows whose count is the magnitude, which the render had been leaving off entirely -
        /// so a plate reading "Warm Yolk  ATK +1" was drawn where the game shows "ATK +1▲".
        /// </summary>
        public static string PlateMeta(EggInstance egg) => PlateMeta(egg, null);

        /// <summary>
        /// The line under the name: trait, any status, any stat stages, and - on your own plate -
        /// whether you are going to move before the thing in front of you.
        ///
        /// Turn order is decided by speed and nothing said what it was. You picked a move without
        /// knowing whether you would live to use it, which matters most in exactly the moment it
        /// is hardest to work out: after a Chill has halved your speed mid-fight.
        /// </summary>
        public static string PlateMeta(EggInstance egg, EggInstance against)
        {
            string order = "";
            if (against != null && !egg.IsFainted && !against.IsFainted)
            {
                order = egg.Spd == against.Spd
                    ? "   <size=18><color=#A8B2C4>coin flip</color></size>"
                    : egg.Spd > against.Spd
                        ? "   <size=18><color=#5FD068>moves first</color></size>"
                        : "   <size=18><color=#E5A055>moves second</color></size>";
            }
            return TypeChart.TraitName(egg.Trait) + StatusTag(egg) + Stages(egg) + order;
        }

        /// <summary>Whether this foe is worth a carton, in the words the plate uses.</summary>
        public static string PlateRecord(EggInstance foe, GameState state, bool isTrainer) =>
            isTrainer ? ""                                      // not yours to take, so the note is noise
            : state.Caught.Contains(foe.Species.Id) ? "Already in your record"
            : "New species — not in your record";

        void RefreshCards(bool resetSprites)
        {
            var foe = Foe;
            foeNameText.text = PlateName(foe);
            foeMetaText.text = PlateMeta(foe);
            foeTypeText.text = TypeChart.Name(foe.Type);
            foeTypeChip.color = TypeChart.ColorOf(foe.Type);
            foeHpBar.SetFraction(foe.HPFraction);
            foeHpBar.SetFillColor(UIKit.HealthColor(foe.HPFraction));
            foeRecordText.text = PlateRecord(foe, State, IsTrainer);
            foeRecordText.color = State.Caught.Contains(foe.Species.Id) ? UIKit.InkDim : UIKit.Accent;

            var mine = Mine;
            myNameText.text = PlateName(mine);
            myMetaText.text = PlateMeta(mine, foe);
            myTypeText.text = TypeChart.Name(mine.Type);
            myTypeChip.color = TypeChart.ColorOf(mine.Type);
            myHpBar.SetFraction(mine.HPFraction);
            myHpBar.SetFillColor(UIKit.HealthColor(mine.HPFraction));
            myHpText.text = UiCopy.Health(mine.CurrentHP, mine.MaxHP);
            myXpBar.SetFraction(mine.XpToNext <= 0 ? 1f : mine.Xp / (float)mine.XpToNext);

            if (resetSprites)
            {
                // Battle portraits are drawn far larger than the world sprites, so use a bigger bake.
                foeEggImage.sprite = ProcArt.Egg(foe.Species, 256);
                foeEggImage.color = Color.white;
                myEggImage.sprite = ProcArt.Egg(mine.Species, 256);
                myEggImage.color = Color.white;
            }
        }

        /// <summary>
        /// What each action does, in the words a player needs at the moment they are choosing.
        ///
        /// The same job the move descriptions do one level down, and the same reason: this is
        /// where the mechanics actually get taught. Ori explains cartons once in the opening
        /// brief and Lune explains salves two sectors later; the menu explains both every time
        /// a player looks at it.
        /// </summary>
        public static readonly string[] ActionHelp =
        {
            "Choose a move.",
            "Throw one. Wear the egg down first — a healthy one kicks straight back out.",
            "Mends the egg in front of you. It costs you the turn.",
            "Bring another egg out. You take a hit on the way in.",
            "Get clear. A faster egg gets away more often.",
        };

        /// <summary>
        /// What a condition is doing to somebody, in the moment a player is choosing what to do
        /// about it.
        ///
        /// The card shows SCORCHED, CHILLED or DAZED, and the line that lands each one explains
        /// it — once. Three turns later the chip is a word with no meaning attached, and the
        /// player is picking a move without being reminded that their egg is moving at half
        /// speed. The chip is the state; this is what the state costs.
        /// </summary>
        public static string ConditionLine(EggInstance egg, bool yours)
        {
            if (egg == null || egg.Status == EggStatus.None) return "";
            string who = yours ? egg.Name : "It";
            switch (egg.Status)
            {
                case EggStatus.Scorched:
                    return "<color=#E5734A>" + who + " is scorched — losing shell every turn.</color>";
                case EggStatus.Chilled:
                    return "<color=#8FE3F2>" + who + " is chilled — moving at half speed.</color>";
                case EggStatus.Dazed:
                    return "<color=#FFC24D>" + who + " is dazed — may lose the turn outright.</color>";
            }
            return "";
        }

        public static string ActionMessageText(int index, EggInstance mine, bool isTrainer)
        {
            if (index < 0 || index >= ActionHelp.Length) return "";

            // The two the game refuses outright against a trainer say why, rather than sitting
            // greyed with no reason given.
            if (isTrainer && index == 1) return "<color=#9AA4B6>Nothing here is yours to take.</color>";
            if (isTrainer && index == 4) return "<color=#9AA4B6>There is no walking away from this one.</color>";

            // A salve on an untouched egg is the one case where the button is greyed for a
            // reason a player might not guess.
            if (index == 2 && mine != null && mine.CurrentHP >= mine.MaxHP)
                return "<color=#9AA4B6>" + mine.Name + " is not hurt.</color>";

            return ActionHelp[index];
        }

        /// <summary>
        /// Whether there is anybody to swap to: another egg in the party, still standing.
        ///
        /// Every other action in this menu greys itself out when it would be refused - the
        /// salve since it was written, the carton and the run more recently. This was the last
        /// one that did not.
        /// </summary>
        public static bool CanSwap(GameState state, int activeIndex)
        {
            for (int i = 0; i < state.Party.Count; i++)
                if (i != activeIndex && !state.Party[i].IsFainted) return true;
            return false;
        }

        /// <summary>
        /// A supply action's label. Empty says so in a word rather than a zero in brackets -
        /// the same reason the supplies strip reads "Cartons none": a 0 among other numbers is
        /// the easiest thing on a screen to read straight past.
        /// </summary>
        public static string SupplyAction(string label, int have) =>
            have > 0 ? label + " (" + have + ")" : label + " (none)";

        void RefreshActionButtons()
        {
            for (int i = 0; i < actionButtons.Count; i++) actionButtons[i].interactable = true;
            UIKit.CaptionOf(actionButtons[1]).text = SupplyAction("CARTON", State.Cartons);
            UIKit.CaptionOf(actionButtons[2]).text = SupplyAction("SALVE", State.Salves);

            // A salve on an untouched egg is a wasted turn, so grey it out rather than
            // letting the player discover the refusal after spending the input.
            actionButtons[2].interactable = State.Salves > 0 && Mine.CurrentHP < Mine.MaxHP;

            // The same for cartons, which was not done: an empty stack stayed selectable and
            // told you it was empty only after you had spent the input on it. The salve beside
            // it had been greying itself out for exactly that reason since it was written.
            actionButtons[1].interactable = State.Cartons > 0;

            // And swapping, which was the last one left. With nobody else able to stand up it
            // took the input and then said "No other egg is in any shape to fight."
            actionButtons[3].interactable = CanSwap(State, activeIndex);

            if (IsTrainer)
            {
                actionButtons[1].interactable = false;   // CARTON
                actionButtons[4].interactable = false;   // RUN
            }
        }

        /// <summary>
        /// The text on a move button. Extracted so the checks can measure what is drawn in a
        /// 286px button and the renderer can draw what the game draws - it had been showing
        /// cards with no effectiveness arrow on them at all, which is the one mark on the card
        /// that changes which move you pick.
        /// </summary>
        public static string MoveCardText(MoveSlot slot, EggType foeType)
        {
            string hex = ColorUtility.ToHtmlStringRGB(TypeChart.ColorOf(slot.Move.Type));
            // Accuracy only shown when it is worth worrying about - a 100% move needs no note.
            string accuracy = slot.Move.Accuracy >= 100 ? ""
                : "  <color=#E5A055>" + slot.Move.Accuracy + "%</color>";

            // A move that leaves something behind says so on the button. Reading it off the
            // flavour text only works if the player already knows to look for it.
            // Its own line. Beside the name it made "Frost Crack  95%  CHILL" 287px in a
            // 286px button; on the end of the stat line it made 34 characters where 32 fit.
            // Abbreviating it to BURN would have fitted and would have contradicted the
            // SCORCHED shown on the card, which is worse than a third line.
            string rider = "";
            switch (BattleCalc.RiderOf(slot.Move.Effect))
            {
                case EggStatus.Scorched: rider = "\n<color=#E5734A>LEAVES SCORCHED</color>"; break;
                case EggStatus.Chilled:  rider = "\n<color=#8FE3F2>LEAVES CHILLED</color>"; break;
                case EggStatus.Dazed:    rider = "\n<color=#FFC24D>LEAVES DAZED</color>"; break;
            }

            // Effectiveness against the egg actually standing there, on every button at once.
            // It was only ever shown for the highlighted move, so comparing four meant arrowing
            // through them one at a time and remembering. An arrow rather than colour alone,
            // because the palette work assumes nothing is carried by hue.
            string edge = "";
            if (!slot.Move.IsStatus)
            {
                float mult = TypeChart.Multiplier(slot.Move.Type, foeType);
                if (TypeChart.IsStrong(mult)) edge = " <color=#FF8A3D>\u25b2</color>";
                else if (TypeChart.IsWeak(mult)) edge = " <color=#9AA4B6>\u25bc</color>";
            }

            return "<color=#" + hex + ">" + slot.Move.Name + "</color>" + accuracy + edge + "\n" +
                   "<size=17><color=#A8B2C4>" + TypeChart.Name(slot.Move.Type) +
                   (slot.Move.IsStatus ? " · STATUS" : " · PWR " + slot.Move.Power) +
                   " · PP " + slot.PP + "/" + slot.Move.MaxPP + "</color>" + rider + "</size>";
        }

        /// <summary>
        /// What the message box says about the move under the cursor. The card carries the
        /// numbers; this carries the sentence - the arrow becomes a word, and the rider becomes
        /// the thing you have to act on.
        /// </summary>
        public static string MoveMessageText(MoveSlot slot, EggType foeType)
        {
            string hex = ColorUtility.ToHtmlStringRGB(TypeChart.ColorOf(slot.Move.Type));
            float mult = TypeChart.Multiplier(slot.Move.Type, foeType);
            string effect = slot.Move.IsStatus ? ""
                // The full name, not the abbreviation. The card abbreviates because it has a
                // 286px button; this box is 1040px and was using half of it to say "TDL" to a
                // player who has no reason yet to know what that stands for.
                : TypeChart.IsStrong(mult) ? "   <color=#FF8A3D>strong against " + TypeChart.Name(foeType) + "</color>"
                : TypeChart.IsWeak(mult) ? "   <color=#9AA4B6>weak against " + TypeChart.Name(foeType) + "</color>"
                : "";
            return "<color=#" + hex + "><b>" + slot.Move.Name + "</b></color>" + effect +
                   "\n<size=24><color=#A8B2C4>" + slot.Move.Describe() + "</color></size>";
        }

        void RefreshMoveButtons()
        {
            var mine = Mine;
            for (int i = 0; i < moveButtons.Count; i++)
            {
                bool has = i < mine.Moves.Count;
                moveButtons[i].gameObject.SetActive(has);
                if (!has) continue;

                var slot = mine.Moves[i];
                UIKit.CaptionOf(moveButtons[i]).text = MoveCardText(slot, Foe.Type);
                moveButtons[i].interactable = slot.Usable;
            }
        }

        /// <summary>
        /// One egg on the swap menu.
        ///
        /// It listed name, level, element and health - everything except the one thing you open
        /// this menu to decide. Swapping means eating a hit on the way in, and the game knows
        /// exactly how that hit lands against each egg on the list; it just never said.
        ///
        /// The read is defensive on purpose. The move cards already show whether your attacks
        /// land; this shows whether you survive the switch, which is the half a player cannot
        /// work out from the cards.
        /// </summary>
        public static string SwapRow(EggInstance egg, EggInstance foe, bool unavailable)
        {
            string hex = ColorUtility.ToHtmlStringRGB(TypeChart.ColorOf(egg.Type));
            string status = egg.IsFainted ? "<color=#E55555>OUT COLD</color>"
                                          : UiCopy.Health(egg.CurrentHP, egg.MaxHP) + " HP";

            string facing = "";
            if (foe != null && !egg.IsFainted && !unavailable)
            {
                float incoming = TypeChart.Multiplier(foe.Type, egg.Type);
                facing = TypeChart.IsStrong(incoming)
                    ? "   <color=#E55555>weak to " + TypeChart.Abbrev(foe.Type) + "</color>"
                    : TypeChart.IsWeak(incoming)
                        ? "   <color=#5FD068>resists " + TypeChart.Abbrev(foe.Type) + "</color>"
                        : "";
            }

            return "  <color=#" + hex + ">●</color>  " + egg.Name +
                   "   <size=19><color=#A8B2C4>Lv " + egg.Level + " · " +
                   TypeChart.Name(egg.Type) + " · </color>" + status + facing + "</size>";
        }

        void RefreshPartyButtons()
        {
            for (int i = 0; i < partyButtons.Count; i++)
            {
                bool has = i < State.Party.Count;
                partyButtons[i].gameObject.SetActive(has);
                if (!has) continue;

                var egg = State.Party[i];
                UIKit.CaptionOf(partyButtons[i]).text =
                    SwapRow(egg, Foe, egg.IsFainted || i == activeIndex);
                partyButtons[i].interactable = !egg.IsFainted && i != activeIndex;
            }
        }

        // ---------- menu navigation ----------

        void ShowMenu(RectTransform panel)
        {
            HideAllMenus();
            UIKit.SetActive(panel, true);
        }

        void HideAllMenus()
        {
            UIKit.SetActive(actionPanel, false);
            UIKit.SetActive(movePanel, false);
            UIKit.SetActive(partyPanel, false);
        }

        void Pick(int index)
        {
            if (activeMenu == null) return;
            if (index < 0 || index >= activeMenu.Count) return;
            if (!activeMenu[index].interactable || !activeMenu[index].gameObject.activeSelf) return;
            pendingChoice = index;
            dir.Audio.Play(Sfx.UiConfirm);
        }

        IEnumerator WaitChoice(List<Button> buttons, int cols, bool cancellable)
        {
            activeMenu = buttons;
            menuCols = Mathf.Max(1, cols);
            allowCancel = cancellable;
            pendingChoice = -1;
            cursor = FirstSelectable(buttons);
            RefreshHighlight();

            while (pendingChoice == -1) yield return null;

            activeMenu = null;
            ClearHighlight(buttons);
        }

        int FirstSelectable(List<Button> buttons)
        {
            for (int i = 0; i < buttons.Count; i++)
                if (buttons[i].gameObject.activeSelf && buttons[i].interactable) return i;
            return 0;
        }

        void Update()
        {
            TickShake();
            TickIdle();
            if (activeMenu == null) return;

            int before = cursor;
            if (EggInput.RightPressed) cursor = Step(cursor, 1);
            if (EggInput.LeftPressed) cursor = Step(cursor, -1);
            if (EggInput.DownPressed) cursor = Step(cursor, menuCols);
            if (EggInput.UpPressed) cursor = Step(cursor, -menuCols);
            if (cursor != before) { RefreshHighlight(); dir.Audio.Play(Sfx.UiMove); }

            int digit = EggInput.DigitPressed;
            if (digit >= 0 && digit < activeMenu.Count) Pick(digit);

            if (EggInput.ConfirmPressed || EggInput.InteractPressed) Pick(cursor);
            if (allowCancel && EggInput.CancelPressed) { pendingChoice = -2; dir.Audio.Play(Sfx.UiBack); }
        }

        bool Selectable(int i) =>
            i >= 0 && i < activeMenu.Count && activeMenu[i].gameObject.activeSelf && activeMenu[i].interactable;

        int Step(int from, int delta)
        {
            int n = activeMenu.Count;

            // Vertical moves walk row by row, taking the same column where it exists and the
            // nearest one where it does not. Stepping by a flat offset instead used to strand
            // the cursor on ragged rows: with three moves known, Down from slot 2 landed on the
            // hidden fourth slot and then ran off the end, so the press did nothing at all.
            if (menuCols > 1 && Mathf.Abs(delta) == menuCols)
            {
                int col = from % menuCols, rows = (n + menuCols - 1) / menuCols;
                int stride = delta > 0 ? 1 : -1;
                for (int r = from / menuCols + stride; r >= 0 && r < rows; r += stride)
                {
                    if (Selectable(r * menuCols + col)) return r * menuCols + col;
                    for (int c = menuCols - 1; c >= 0; c--)
                        if (Selectable(r * menuCols + c)) return r * menuCols + c;
                }
                return from;
            }

            for (int i = 1; i <= n; i++)
            {
                int candidate = from + delta * i;
                if (candidate < 0 || candidate >= n) continue;
                if (Selectable(candidate)) return candidate;
            }
            return from;
        }

        void RefreshHighlight()
        {
            if (activeMenu == null) return;

            // While browsing moves, the message box explains whichever one is highlighted.
            if (activeMenu == moveButtons && cursor < Mine.Moves.Count)
            {
                messageText.text = MoveMessageText(Mine.Moves[cursor], Foe.Type);
            }
            // And the same one level up. The action menu had this box carrying a single line -
            // "What will Pebbles do?" - above six hundred pixels of nothing, on the screen a
            // player makes every decision in the game on. The move menu below it has explained
            // its options since it was written.
            else if (activeMenu == actionButtons && cursor < ActionHelp.Length)
            {
                // Yours first: it is the one you can do something about this turn.
                string mine = ConditionLine(Mine, true);
                string theirs = ConditionLine(Foe, false);
                string conditions = mine + (mine.Length > 0 && theirs.Length > 0 ? "\n" : "") + theirs;

                messageText.text = ActionMessageText(cursor, Mine, IsTrainer) +
                                   (conditions.Length > 0 ? "\n" + conditions : "");
            }

            for (int i = 0; i < activeMenu.Count; i++)
            {
                var img = activeMenu[i].image;
                if (img == null) continue;
                bool selected = i == cursor;
                img.color = selected ? new Color32(0x46, 0x3C, 0x18, 0xFF) : UIKit.PanelLight;
                var caption = UIKit.CaptionOf(activeMenu[i]);
                if (caption != null) caption.color = selected ? UIKit.Accent : UIKit.Ink;
            }
        }

        void ClearHighlight(List<Button> buttons)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i].image != null) buttons[i].image.color = UIKit.PanelLight;
                var caption = UIKit.CaptionOf(buttons[i]);
                if (caption != null) caption.color = UIKit.Ink;
            }
        }

        bool AnyAliveInParty() => CountAliveInParty() > 0;

        int CountAliveInParty()
        {
            int n = 0;
            for (int i = 0; i < State.Party.Count; i++)
                if (!State.Party[i].IsFainted) n++;
            return n;
        }
    }
}

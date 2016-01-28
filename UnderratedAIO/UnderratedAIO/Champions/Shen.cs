using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using UnderratedAIO.Helpers;
using Color = System.Drawing.Color;
using Orbwalking = UnderratedAIO.Helpers.Orbwalking;

namespace UnderratedAIO.Champions
{
    internal class Shen
    {
        public static Menu config;
        private static Orbwalking.Orbwalker orbwalker;
        private static readonly Obj_AI_Hero player = ObjectManager.Player;
        public static Spell Q, W, E, EFlash, R;
        private static float bladeRadius = 325f;
        public static bool PingCasted = false;
        private const int XOffset = 36;
        private const int YOffset = 9;
        private const int Width = 103;
        private const int Height = 8;
        public static Vector3 blade, bladeOnCast;

        private static readonly Render.Text Text = new Render.Text(
            0, 0, "", 11, new ColorBGRA(255, 0, 0, 255), "monospace");

        public static AutoLeveler autoLeveler;

        public Shen()
        {
            InitShen();
            InitMenu();
            Game.PrintChat("<font color='#9933FF'>Soresu </font><font color='#FFFFFF'>- Shen</font>");
            Game.OnUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Game_OnDraw;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Interrupter2.OnInterruptableTarget += OnPossibleToInterrupt;
            Obj_AI_Base.OnDamage += Obj_AI_Base_OnDamage;
            Obj_AI_Base.OnProcessSpellCast += Game_ProcessSpell;
            Jungle.setSmiteSlot();
            Utility.HpBarDamageIndicator.DamageToUnit = ComboDamage;
        }

        private void Obj_AI_Base_OnDamage(AttackableUnit sender, AttackableUnitDamageEventArgs args)
        {
            var t = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(h => h.NetworkId == args.SourceNetworkId);
            var s = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(h => h.NetworkId == args.TargetNetworkId);
            if (t != null && s != null &&
                (t.IsMe &&
                 ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tw => tw.Distance(t) < 750 && tw.Distance(s) < 750) !=
                 null))
            {
                if (config.Item("autotauntattower", true).GetValue<bool>() && E.CanCast(s))
                {
                    E.Cast(s, config.Item("packets").GetValue<bool>());
                }
            }
        }


        private void OnPossibleToInterrupt(Obj_AI_Hero unit, Interrupter2.InterruptableTargetEventArgs args)
        {
            if (!config.Item("useeint", true).GetValue<bool>())
            {
                return;
            }
            if (unit.IsValidTarget(E.Range) && E.IsReady())
            {
                E.Cast(unit, config.Item("packets").GetValue<bool>());
            }
        }

        private static void Game_OnDraw(EventArgs args)
        {
            DrawHelper.DrawCircle(config.Item("drawqq", true).GetValue<Circle>(), Q.Range);
            DrawHelper.DrawCircle(config.Item("drawee", true).GetValue<Circle>(), E.Range);
            DrawHelper.DrawCircle(config.Item("draweeflash", true).GetValue<Circle>(), EFlash.Range);
            Helpers.Jungle.ShowSmiteStatus(
                config.Item("useSmite").GetValue<KeyBind>().Active, config.Item("smiteStatus").GetValue<bool>());
            if (config.Item("drawallyhp", true).GetValue<bool>())
            {
                DrawHealths();
            }
            if (config.Item("drawincdmg", true).GetValue<bool>())
            {
                getIncDmg();
            }
            if (true)
            {
                Render.Circle.DrawCircle(blade, bladeRadius, Color.BlueViolet, 7);
            }
            Utility.HpBarDamageIndicator.Enabled = config.Item("drawcombo", true).GetValue<bool>();
        }

        private static void DrawHealths()
        {
            float i = 0;
            foreach (
                var hero in ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.IsAlly && !hero.IsMe && !hero.IsDead))
            {
                var playername = hero.Name;
                if (playername.Length > 13)
                {
                    playername = playername.Remove(9) + "...";
                }
                var champion = hero.SkinName;
                if (champion.Length > 12)
                {
                    champion = champion.Remove(7) + "...";
                }
                var percent = (int) (hero.Health / hero.MaxHealth * 100);
                var color = Color.Red;
                if (percent > 25)
                {
                    color = Color.Orange;
                }
                if (percent > 50)
                {
                    color = Color.Yellow;
                }
                if (percent > 75)
                {
                    color = Color.LimeGreen;
                }
                Drawing.DrawText(
                    Drawing.Width * 0.8f, Drawing.Height * 0.1f + i, color, playername + "(" + champion + ")");
                Drawing.DrawText(
                    Drawing.Width * 0.9f, Drawing.Height * 0.1f + i, color,
                    ((int) hero.Health).ToString() + " (" + percent.ToString() + "%)");
                i += 20f;
            }
        }

        private static void getIncDmg()
        {
            var color = Color.Red;
            float result = CombatHelper.getIncDmg();
            var barPos = player.HPBarPosition;
            var damage = (float) result;
            if (damage == 0)
            {
                return;
            }
            var percentHealthAfterDamage = Math.Max(0, player.Health - damage) / player.MaxHealth;
            var xPos = barPos.X + XOffset + Width * percentHealthAfterDamage;

            if (damage > player.Health)
            {
                Text.X = (int) barPos.X + XOffset;
                Text.Y = (int) barPos.Y + YOffset - 13;
                Text.text = ((int) (player.Health - damage)).ToString();
                Text.OnEndScene();
            }

            Drawing.DrawLine(xPos, barPos.Y + YOffset, xPos, barPos.Y + YOffset + Height, 3, color);
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            Ulti();
            if (config.Item("useeflash", true).GetValue<KeyBind>().Active &&
                player.Spellbook.CanUseSpell(player.GetSpellSlot("SummonerFlash")) == SpellState.Ready)
            {
                FlashCombo();
            }
            switch (orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                    Combo();
                    break;
                case Orbwalking.OrbwalkingMode.Mixed:
                    Harass();
                    break;
                case Orbwalking.OrbwalkingMode.LaneClear:
                    Clear();
                    break;
                case Orbwalking.OrbwalkingMode.LastHit:
                    break;
                default:
                    break;
            }
            Jungle.CastSmite(config.Item("useSmite").GetValue<KeyBind>().Active);

            var bladeObj =
                ObjectManager.Get<Obj_AI_Minion>()
                    .Where(
                        o => (o.Name == "ShenThingUnit" || o.Name == "ShenArrowVfxHostMinion") && o.Team == player.Team)
                    .OrderBy(o => o.Distance(bladeOnCast))
                    .FirstOrDefault();
            if (bladeObj != null)
            {
                blade = bladeObj.Position;
            }
            if (W.IsReady() && blade.IsValid())
            {
                foreach (var ally in HeroManager.Allies.Where(a => a.Distance(blade) < bladeRadius))
                {
                    var data = Program.IncDamages.GetAllyData(ally.NetworkId);
                    if (config.Item("autowAgg", true).GetValue<Slider>().Value <= data.AADamageCount)
                    {
                        W.Cast();
                    }
                    if (data.AADamageTaken >= 50 && config.Item("autow", true).GetValue<bool>())
                    {
                        W.Cast();
                    }
                }
            }
        }

        private static bool HasShield
        {
            get { return player.HasBuff("shenpassiveshield"); }
        }

        private static void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!config.Item("useeagc", true).GetValue<bool>())
            {
                return;
            }
            if (gapcloser.Sender.IsValidTarget(E.Range) && E.IsReady() &&
                player.Distance(gapcloser.Sender.Position) < 400)
            {
                E.Cast(gapcloser.End, config.Item("packets").GetValue<bool>());
            }
        }

        private static void Clear()
        {
            var minionsHP = ObjectManager.Get<Obj_AI_Minion>().Where(m => m.IsValidTarget(400)).Sum(m => m.Health);

            if (config.Item("useqLC", true).GetValue<bool>() && minionsHP > 300)
            {
                Q.Cast();
            }
        }

        private static void Ulti()
        {
            if (!R.IsReady() || PingCasted || player.IsDead)
            {
                return;
            }

            foreach (var allyObj in
                ObjectManager.Get<Obj_AI_Hero>()
                    .Where(
                        i =>
                            i.IsAlly && !i.IsMe && !i.IsDead &&
                            ((Checkinrange(i) &&
                              ((i.Health * 100 / i.MaxHealth) <= config.Item("atpercent", true).GetValue<Slider>().Value) ||
                              Program.IncDamages.GetAllyData(i.NetworkId).DamageTaken > i.Health) ||
                             (CombatHelper.CheckCriticalBuffs(i) && i.CountEnemiesInRange(600) < 1))))
            {
                if (config.Item("user", true).GetValue<bool>() &&
                    orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.Combo && R.IsReady() &&
                    player.CountEnemiesInRange((int) E.Range) < 1 &&
                    !config.Item("ult" + allyObj.SkinName).GetValue<bool>())
                {
                    R.Cast(allyObj);
                    return;
                }
                else
                {
                    DrawHelper.popUp("Use R to help " + allyObj.ChampionName, 3000, Color.Red, Color.White, Color.Red);
                }
                PingCasted = true;
                Utility.DelayAction.Add(5000, () => PingCasted = false);
            }
        }

        private static bool Checkinrange(Obj_AI_Hero i)
        {
            if (i.CountEnemiesInRange(750) >= 1 && i.CountEnemiesInRange(750) < 3)
            {
                return true;
            }
            return false;
        }

        private static void Harass()
        {
            Obj_AI_Hero target = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);
            if (target != null && Q.IsReady() && config.Item("harassq", true).GetValue<bool>() &&
                Orbwalking.CanMove(100))
            {
                HandleQ(target);
            }
        }

        private static void Combo()
        {
            var minHit = config.Item("useemin", true).GetValue<Slider>().Value;
            Obj_AI_Hero target = TargetSelector.GetTarget(E.Range + 400, TargetSelector.DamageType.Magical);
            if (target == null)
            {
                return;
            }
            if (config.Item("useItems").GetValue<bool>())
            {
                ItemHandler.UseItems(target, config, ComboDamage(target));
            }
            var useE = config.Item("usee", true).GetValue<bool>() && E.IsReady() &&
                       player.Distance(target.Position) < E.Range;
            if (useE)
            {
                if (minHit > 1)
                {
                    CastEmin(target, minHit);
                }
                else if ((player.Distance(target.Position) > Orbwalking.GetRealAutoAttackRange(target) ||
                          player.HealthPercent < 45 || player.CountEnemiesInRange(1000) == 1) &&
                         E.GetPrediction(target).Hitchance >= HitChance.High)
                {
                    E.Cast(target, config.Item("packets").GetValue<bool>());
                }
            }
            bool hasIgnite = player.Spellbook.CanUseSpell(player.GetSpellSlot("SummonerDot")) == SpellState.Ready;
            var ignitedmg = (float) player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);
            if (config.Item("useIgnite").GetValue<bool>() && ignitedmg > target.Health && hasIgnite &&
                !E.CanCast(target) && !Q.CanCast(target))
            {
                player.Spellbook.CastSpell(player.GetSpellSlot("SummonerDot"), target);
            }
            if (Q.IsReady() && config.Item("useq", true).GetValue<bool>() && Orbwalking.CanMove(100))
            {
                HandleQ(target);
            }
        }

        private static void HandleQ(Obj_AI_Hero target)
        {
            Q.UpdateSourcePosition(blade);
            var pred = Q.GetPrediction(target);
            var poly = CombatHelper.GetPoly(blade, player.Distance(blade), 150);
            if ((pred.Hitchance >= HitChance.High && poly.IsInside(pred.UnitPosition)) || (target.Distance(blade) < 100) ||
                (target.Distance(blade) < 500 && poly.IsInside(target.Position)) ||
                player.Distance(target) < Orbwalking.GetRealAutoAttackRange(target))
            {
                Q.Cast();
            }
        }

        public static void CastEmin(Obj_AI_Base target, int min)
        {
            var MaxEnemy = player.CountEnemiesInRange(1580);
            if (MaxEnemy == 1)
            {
                E.Cast(target);
            }
            else
            {
                var MinEnemy = Math.Min(min, MaxEnemy);
                foreach (var enemy in
                    ObjectManager.Get<Obj_AI_Hero>()
                        .Where(i => i.Distance(player) < E.Range && i.IsEnemy && !i.IsDead && i.IsValidTarget()))
                {
                    for (int i = MaxEnemy; i > MinEnemy - 1; i--)
                    {
                        if (E.CastIfWillHit(enemy, i))
                        {
                            return;
                        }
                    }
                }
            }
        }

        private static void FlashCombo()
        {
            Obj_AI_Hero target = TargetSelector.GetTarget(EFlash.Range, TargetSelector.DamageType.Magical);
            if (config.Item("usee", true).GetValue<bool>() && E.IsReady() &&
                player.Distance(target.Position) < EFlash.Range && player.Distance(target.Position) > 480 &&
                !((getPosToEflash(target.Position)).IsWall()))
            {
                player.Spellbook.CastSpell(player.GetSpellSlot("SummonerFlash"), getPosToEflash(target.Position));

                E.Cast(target.Position, config.Item("packets").GetValue<bool>());
            }
            if (Q.IsReady() && config.Item("useq").GetValue<bool>())
            {
                Q.CastOnUnit(target, config.Item("packets").GetValue<bool>());
            }
            ItemHandler.UseItems(target, config);
        }

        public static Vector3 getPosToEflash(Vector3 target)
        {
            return target + (player.Position - target) / 2;
        }

        private static float ComboDamage(Obj_AI_Hero hero)
        {
            float damage = 0;
            if (Q.IsReady() && player.Spellbook.GetSpell(SpellSlot.Q).ManaCost < player.Mana)
            {
                damage += (float) Damage.GetSpellDamage(player, hero, SpellSlot.Q);
            }
            if (E.IsReady() && player.Spellbook.GetSpell(SpellSlot.E).ManaCost < player.Mana)
            {
                damage += (float) Damage.GetSpellDamage(player, hero, SpellSlot.E);
            }
            if (player.Spellbook.CanUseSpell(player.GetSpellSlot("summonerdot")) == SpellState.Ready &&
                hero.Health - damage < (float) player.GetSummonerSpellDamage(hero, Damage.SummonerSpell.Ignite))
            {
                damage += (float) player.GetSummonerSpellDamage(hero, Damage.SummonerSpell.Ignite);
            }
            return damage;
        }

        private void Game_ProcessSpell(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (args.SData.Name == "ShenQ" || args.SData.Name == "ShenR")
            {
                bladeOnCast = args.End;
            }
        }

        private static void InitShen()
        {
            Q = new Spell(SpellSlot.Q);
            Q.SetSkillshot(0.5f, 150f, 2500f, false, SkillshotType.SkillshotLine);
            W = new Spell(SpellSlot.W); //2500f
            E = new Spell(SpellSlot.E, 600);
            E.SetSkillshot(0.5f, 50f, 1600f, false, SkillshotType.SkillshotLine);
            EFlash = new Spell(SpellSlot.E, 990);
            EFlash.SetSkillshot(
                E.Instance.SData.SpellCastTime, E.Instance.SData.LineWidth, E.Speed, false, SkillshotType.SkillshotLine);
            R = new Spell(SpellSlot.R, float.MaxValue);
        }

        private static void InitMenu()
        {
            config = new Menu("Shen", "SRS_Shen", true);
            // Target Selector
            Menu menuTS = new Menu("Selector", "tselect");
            TargetSelector.AddToMenu(menuTS);
            config.AddSubMenu(menuTS);

            // Orbwalker
            Menu menuOrb = new Menu("Orbwalker", "orbwalker");
            orbwalker = new Orbwalking.Orbwalker(menuOrb);
            config.AddSubMenu(menuOrb);
            // Draw settings
            Menu menuD = new Menu("Drawings ", "dsettings");
            menuD.AddItem(new MenuItem("drawqq", "Draw Q range", true))
                .SetValue(new Circle(false, Color.FromArgb(150, 150, 62, 172)));
            menuD.AddItem(new MenuItem("drawee", "Draw E range", true))
                .SetValue(new Circle(false, Color.FromArgb(150, 150, 62, 172)));
            menuD.AddItem(new MenuItem("draweeflash", "Draw E+flash range", true))
                .SetValue(new Circle(true, Color.FromArgb(50, 250, 248, 110)));
            menuD.AddItem(new MenuItem("drawallyhp", "Draw teammates' HP", true)).SetValue(true);
            menuD.AddItem(new MenuItem("drawincdmg", "Draw incoming damage", true)).SetValue(true);
            menuD.AddItem(new MenuItem("drawcombo", "Draw combo damage", true)).SetValue(true);
            config.AddSubMenu(menuD);

            // Combo Settings
            Menu menuC = new Menu("Combo ", "csettings");
            menuC.AddItem(new MenuItem("useq", "Use Q", true)).SetValue(true);
            menuC.AddItem(new MenuItem("usew", "Use W", true)).SetValue(true);
            menuC.AddItem(new MenuItem("usee", "Use E", true)).SetValue(true);
            menuC.AddItem(new MenuItem("useeflash", "Flash+E", true))
                .SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press))
                .SetFontStyle(System.Drawing.FontStyle.Bold, SharpDX.Color.Orange);
            menuC.AddItem(new MenuItem("useemin", "   Min target in teamfight", true)).SetValue(new Slider(1, 1, 5));
            menuC.AddItem(new MenuItem("useIgnite", "Use Ignite")).SetValue(true);
            menuC = ItemHandler.addItemOptons(menuC);
            config.AddSubMenu(menuC);

            // Harass Settings
            Menu menuH = new Menu("Harass ", "hsettings");
            menuH.AddItem(new MenuItem("harassq", "Harass with Q", true)).SetValue(true);
            config.AddSubMenu(menuH);
            // LaneClear Settings
            Menu menuLC = new Menu("LaneClear ", "Lcsettings");
            menuLC.AddItem(new MenuItem("useqLC", "Use Q", true)).SetValue(true);
            config.AddSubMenu(menuLC);
            // Misc Settings
            Menu menuU = new Menu("Misc ", "usettings");
            menuU.AddItem(new MenuItem("autow", "Auto block AA with W", true)).SetValue(true);
            menuU.AddItem(new MenuItem("autowAgg", "W on aggro", true)).SetValue(new Slider(4, 1, 10));
            menuU.AddItem(new MenuItem("autotauntattower", "Auto taunt in tower range", true)).SetValue(true);
            menuU.AddItem(new MenuItem("useeagc", "Use E to anti gap closer", true)).SetValue(false);
            menuU.AddItem(new MenuItem("useeint", "Use E to interrupt", true)).SetValue(true);
            menuU.AddItem(new MenuItem("user", "Use R", true)).SetValue(true);
            menuU.AddItem(new MenuItem("atpercent", "   Under % health", true)).SetValue(new Slider(20, 0, 100));
            menuU = Jungle.addJungleOptions(menuU);


            Menu autolvlM = new Menu("AutoLevel", "AutoLevel");
            autoLeveler = new AutoLeveler(autolvlM);
            menuU.AddSubMenu(autolvlM);

            config.AddSubMenu(menuU);
            var sulti = new Menu("Don't ult on ", "dontult");
            foreach (var hero in ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.IsAlly))
            {
                if (hero.SkinName != player.SkinName)
                {
                    sulti.AddItem(new MenuItem("ult" + hero.SkinName, hero.SkinName)).SetValue(false);
                }
            }
            config.AddSubMenu(sulti);
            config.AddItem(new MenuItem("packets", "Use Packets")).SetValue(false);
            config.AddItem(new MenuItem("UnderratedAIO", "by Soresu v" + Program.version.ToString().Replace(",", ".")));
            config.AddToMainMenu();
        }
    }
}
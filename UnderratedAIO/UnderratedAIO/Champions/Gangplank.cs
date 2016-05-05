using System;
using System.Collections.Generic;
using System.Linq;
using Color = System.Drawing.Color;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using UnderratedAIO.Helpers;
using Environment = UnderratedAIO.Helpers.Environment;
using Orbwalking = UnderratedAIO.Helpers.Orbwalking;

namespace UnderratedAIO.Champions
{
    internal class Gangplank
    {
        public static Menu config;
        public static Orbwalking.Orbwalker orbwalker;
        public static AutoLeveler autoLeveler;
        public static Spell Q, W, E, R;
        public static readonly Obj_AI_Hero player = ObjectManager.Player;
        public static bool justQ, justE;
        public Vector3 ePos;
        public const int BarrelExplosionRange = 325;
        public const int BarrelConnectionRange = 660;
        public List<Barrel> savedBarrels = new List<Barrel>();
        public double[] Rwave = new double[] { 50, 70, 90 };
        public double[] EDamage = new double[] { 60, 90, 120, 150, 180 };

        public Gangplank()
        {
            InitGangPlank();
            InitMenu();
            Game.PrintChat("<font color='#9933FF'>Soresu </font><font color='#FFFFFF'>- Gangplank</font>");
            Drawing.OnDraw += Game_OnDraw;
            Game.OnUpdate += Game_OnGameUpdate;
            Helpers.Jungle.setSmiteSlot();
            Utility.HpBarDamageIndicator.DamageToUnit = ComboDamage;
            Obj_AI_Base.OnProcessSpellCast += Game_ProcessSpell;
            GameObject.OnCreate += GameObjectOnOnCreate;
            GameObject.OnDelete += GameObject_OnDelete;
        }


        private void GameObject_OnDelete(GameObject sender, EventArgs args)
        {
            for (int i = 0; i < savedBarrels.Count; i++)
            {
                if (savedBarrels[i].barrel.NetworkId == sender.NetworkId || savedBarrels[i].barrel.IsDead)
                {
                    savedBarrels.RemoveAt(i);
                    return;
                }
            }
        }

        private void GameObjectOnOnCreate(GameObject sender, EventArgs args)
        {
            if (sender.Name == "Barrel")
            {
                savedBarrels.Add(new Barrel(sender as Obj_AI_Minion, System.Environment.TickCount));
            }
        }

        private IEnumerable<Obj_AI_Minion> GetBarrels()
        {
            return savedBarrels.Select(b => b.barrel).Where(b => b.IsValid);
        }

        private bool KillableBarrel(Obj_AI_Base targetB,
            bool melee = false,
            Obj_AI_Hero sender = null,
            float missileTravelTime = -1)
        {
            if (targetB.Health < 2)
            {
                return true;
            }
            if (sender == null)
            {
                sender = player;
            }
            if (missileTravelTime == -1)
            {
                missileTravelTime = GetQTime(targetB);
            }
            var barrel = savedBarrels.FirstOrDefault(b => b.barrel.NetworkId == targetB.NetworkId);
            if (barrel != null)
            {
                var time = targetB.Health * getEActivationDelay() * 1000;
                if (System.Environment.TickCount - barrel.time + (melee ? sender.AttackDelay : missileTravelTime) * 1000 >
                    time)
                {
                    return true;
                }
            }
            return false;
        }

        private float GetQTime(Obj_AI_Base targetB)
        {
            return player.Distance(targetB) / 2800f + Q.Delay;
        }

        private void InitGangPlank()
        {
            Q = new Spell(SpellSlot.Q, 590f); //2600f
            Q.SetTargetted(0.25f, 2200f);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E, 950);
            E.SetSkillshot(0.8f, 50, float.MaxValue, false, SkillshotType.SkillshotCircle);
            R = new Spell(SpellSlot.R);
            R.SetSkillshot(1f, 100, float.MaxValue, false, SkillshotType.SkillshotCircle);
        }

        private void Game_OnGameUpdate(EventArgs args)
        {
            orbwalker.SetAttack(true);
            orbwalker.SetMovement(true);
            Jungle.CastSmite(config.Item("useSmite").GetValue<KeyBind>().Active);
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
                    if (config.Item("useqLHH", true).GetValue<bool>() && !justE)
                    {
                        Lasthit();
                    }
                    break;
                default:
                    break;
            }
            if (config.Item("AutoR", true).GetValue<bool>() && R.IsReady())
            {
                foreach (var enemy in
                    HeroManager.Enemies.Where(
                        e =>
                            ((e.UnderTurret(true) &&
                              e.MaxHealth / 100 * config.Item("Rhealt", true).GetValue<Slider>().Value * 0.75f >
                              e.Health - Program.IncDamages.GetEnemyData(e.NetworkId).DamageTaken) ||
                             (!e.UnderTurret(true) &&
                              e.MaxHealth / 100 * config.Item("Rhealt", true).GetValue<Slider>().Value >
                              e.Health - Program.IncDamages.GetEnemyData(e.NetworkId).DamageTaken)) &&
                            e.HealthPercent > config.Item("RhealtMin", true).GetValue<Slider>().Value &&
                            e.IsValidTarget() && e.Distance(player) > 1500))
                {
                    var pred = Program.IncDamages.GetEnemyData(enemy.NetworkId);
                    if (pred != null && pred.DamageTaken < enemy.Health)
                    {
                        var ally =
                            HeroManager.Allies.OrderBy(a => a.Health).FirstOrDefault(a => enemy.Distance(a) < 1000);
                        if (ally != null)
                        {
                            var pos = Prediction.GetPrediction(enemy, 0.75f);
                            if (pos.CastPosition.Distance(enemy.Position) < 450 && pos.Hitchance >= HitChance.VeryHigh)
                            {
                                if (enemy.IsMoving)
                                {
                                    R.Cast(enemy.Position.Extend(pos.CastPosition, 450));
                                }
                                else
                                {
                                    R.Cast(enemy.ServerPosition);
                                }
                            }
                        }
                    }
                }
            }
            if (config.Item("EQtoCursor", true).GetValue<KeyBind>().Active && E.IsReady() && Q.IsReady())
            {
                orbwalker.SetMovement(false);

                var barrel =
                    GetBarrels()
                        .Where(
                            o =>
                                o.IsValid && !o.IsDead && o.Distance(player) < Q.Range &&
                                o.SkinName == "GangplankBarrel" && o.GetBuff("gangplankebarrellife").Caster.IsMe &&
                                KillableBarrel(o))
                        .OrderBy(o => o.Distance(Game.CursorPos))
                        .FirstOrDefault();
                if (barrel != null)
                {
                    var cp = Game.CursorPos;
                    var cursorPos = barrel.Distance(cp) > BarrelConnectionRange
                        ? barrel.Position.Extend(cp, BarrelConnectionRange)
                        : cp;
                    var points =
                        CombatHelper.PointsAroundTheTarget(player.Position, E.Range - 200, 15, 6)
                            .Where(p => p.Distance(player.Position) < E.Range);
                    var cursorPos2 = cursorPos.Distance(cp) > BarrelConnectionRange
                        ? cursorPos.Extend(cp, BarrelConnectionRange)
                        : cp;
                    var middle = GetMiddleBarrel(barrel, points, cursorPos);
                    var threeBarrel = cursorPos.Distance(cp) > BarrelExplosionRange && E.Instance.Ammo >= 2 &&
                                      Game.CursorPos.Distance(player.Position) < E.Range && middle.IsValid();
                    var firsDelay = threeBarrel ? 500 : 1;
                    if (cursorPos.IsValid() && cursorPos.Distance(player.Position) < E.Range)
                    {
                        E.Cast(threeBarrel ? middle : cursorPos);
                        Utility.DelayAction.Add(firsDelay, () => Q.CastOnUnit(barrel));
                        if (threeBarrel)
                        {
                            if (player.IsMoving)
                            {
                                player.IssueOrder(GameObjectOrder.Stop, player.Position);
                            }
                            Utility.DelayAction.Add(801, () => E.Cast(middle.Extend(cp, BarrelConnectionRange)));
                        }
                        else
                        {
                            if (Orbwalking.CanMove(100))
                            {
                                orbwalker.SetMovement(true);
                                Orbwalking.MoveTo(Game.CursorPos);
                            }
                        }
                    }
                }
                else
                {
                    if (Orbwalking.CanMove(100))
                    {
                        orbwalker.SetMovement(true);
                        Orbwalking.MoveTo(Game.CursorPos);
                    }
                }
            }
            else if (config.Item("EQtoCursor", true).GetValue<KeyBind>().Active)
            {
                if (Orbwalking.CanMove(100))
                {
                    orbwalker.SetMovement(true);
                    Orbwalking.MoveTo(Game.CursorPos);
                }
            }
            if (config.Item("QbarrelCursor", true).GetValue<KeyBind>().Active && Q.IsReady())
            {
                var meleeRangeBarrel =
                    GetBarrels()
                        .OrderBy(o => o.Distance(Game.CursorPos))
                        .FirstOrDefault(
                            o =>
                                o.Health > 1 && o.Distance(player) < Orbwalking.GetRealAutoAttackRange(o) &&
                                !KillableBarrel(o, true));
                if (meleeRangeBarrel != null && Orbwalking.CanAttack())
                {
                    orbwalker.SetAttack(false);
                    player.IssueOrder(GameObjectOrder.AttackUnit, meleeRangeBarrel);
                    return;
                }
                var barrel =
                    GetBarrels()
                        .Where(
                            o =>
                                o.IsValid && !o.IsDead && o.Distance(player) < Q.Range &&
                                o.SkinName == "GangplankBarrel" && o.GetBuff("gangplankebarrellife").Caster.IsMe &&
                                KillableBarrel(o))
                        .OrderBy(o => o.Distance(Game.CursorPos))
                        .FirstOrDefault();
                if (barrel != null)
                {
                    Q.CastOnUnit(barrel);
                }
            }

            if (config.Item("AutoQBarrel", true).GetValue<bool>() && Q.IsReady())
            {
                var barrel =
                    GetBarrels()
                        .FirstOrDefault(
                            o =>
                                o.IsValid && !o.IsDead && o.Distance(player) < Q.Range &&
                                o.SkinName == "GangplankBarrel" && o.GetBuff("gangplankebarrellife").Caster.IsMe &&
                                KillableBarrel(o) &&
                                (o.CountEnemiesInRange(BarrelExplosionRange - 50) > 0 ||
                                 HeroManager.Enemies.Count(
                                     e =>
                                         e.IsValidTarget() &&
                                         Prediction.GetPrediction(e, 0.1f).UnitPosition.Distance(o.Position) <
                                         BarrelExplosionRange - 20) > 0));

                if (barrel != null)
                {
                    Q.Cast(barrel);
                }
            }
        }

        private Vector3 GetMiddleBarrel(Obj_AI_Minion barrel, IEnumerable<Vector3> points, Vector3 cursorPos)
        {
            var middle =
                points.Where(
                    p =>
                        !p.IsWall() && p.Distance(barrel.Position) < BarrelConnectionRange &&
                        p.Distance(barrel.Position) > BarrelExplosionRange &&
                        p.Distance(cursorPos) < BarrelConnectionRange && p.Distance(cursorPos) > BarrelExplosionRange &&
                        p.Distance(barrel.Position) + p.Distance(cursorPos) > BarrelExplosionRange * 2 - 100)
                    .OrderByDescending(p => p.CountEnemiesInRange(BarrelExplosionRange))
                    .ThenByDescending(p => p.Distance(barrel.Position))
                    .FirstOrDefault();
            return middle;
        }

        private void Lasthit()
        {
            if (Q.IsReady())
            {
                var mini =
                    MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly)
                        .Where(m => m.Health < Q.GetDamage(m) && m.SkinName != "GangplankBarrel")
                        .OrderByDescending(m => m.MaxHealth)
                        .ThenByDescending(m => m.Distance(player))
                        .FirstOrDefault();

                if (mini != null && !justE)
                {
                    Q.CastOnUnit(mini, config.Item("packets").GetValue<bool>());
                }
            }
        }


        private void Harass()
        {
            float perc = config.Item("minmanaH", true).GetValue<Slider>().Value / 100f;
            if (player.Mana < player.MaxMana * perc)
            {
                return;
            }
            Obj_AI_Hero target = TargetSelector.GetTarget(
                Q.Range + BarrelExplosionRange, TargetSelector.DamageType.Physical);
            var barrel =
                GetBarrels()
                    .FirstOrDefault(
                        o =>
                            target != null && o.IsValid && !o.IsDead && o.Distance(player) < Q.Range &&
                            o.SkinName == "GangplankBarrel" && o.GetBuff("gangplankebarrellife").Caster.IsMe &&
                            KillableBarrel(o) && o.Distance(target) < BarrelExplosionRange);

            if (barrel != null)
            {
                Q.CastOnUnit(barrel, config.Item("packets").GetValue<bool>());
                return;
            }
            if (config.Item("useqLHH", true).GetValue<bool>())
            {
                var mini =
                    MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly)
                        .Where(m => m.Health < Q.GetDamage(m) && m.SkinName != "GangplankBarrel")
                        .OrderByDescending(m => m.MaxHealth)
                        .ThenByDescending(m => m.Distance(player))
                        .FirstOrDefault();

                if (mini != null)
                {
                    Q.CastOnUnit(mini, config.Item("packets").GetValue<bool>());
                    return;
                }
            }

            if (target == null)
            {
                return;
            }
            if (config.Item("useqH", true).GetValue<bool>() && Q.CanCast(target) && !justE)
            {
                var barrels =
                    GetBarrels()
                        .Where(
                            o =>
                                o.IsValid && !o.IsDead && o.Distance(player) < 1600 && o.SkinName == "GangplankBarrel" &&
                                o.GetBuff("gangplankebarrellife").Caster.IsMe)
                        .ToList();
                CastQonHero(target, barrels);
            }
            if (config.Item("useeH", true).GetValue<bool>() && Q.CanCast(target) &&
                config.Item("eStacksH", true).GetValue<Slider>().Value < E.Instance.Ammo)
            {
                CastEtarget(target);
            }
        }

        private void Clear()
        {
            float perc = config.Item("minmana", true).GetValue<Slider>().Value / 100f;
            if (player.Mana < player.MaxMana * perc)
            {
                return;
            }
            if (Q.IsReady() && Q.IsReady() && config.Item("useqLC", true).GetValue<bool>())
            {
                var barrel =
                    GetBarrels()
                        .FirstOrDefault(
                            o =>
                                o.IsValid && !o.IsDead && o.Distance(player) < Q.Range &&
                                o.SkinName == "GangplankBarrel" && o.GetBuff("gangplankebarrellife").Caster.IsMe &&
                                KillableBarrel(o) &&
                                Environment.Minion.countMinionsInrange(o.Position, BarrelExplosionRange) >=
                                config.Item("eMinHit", true).GetValue<Slider>().Value);
                if (barrel != null)
                {
                    Q.CastOnUnit(barrel, config.Item("packets").GetValue<bool>());
                    return;
                }
            }
            if (config.Item("useqLC", true).GetValue<bool>() && !justE)
            {
                Lasthit();
            }
            if (config.Item("useeLC", true).GetValue<bool>() && E.IsReady() &&
                config.Item("eStacksLC", true).GetValue<Slider>().Value < E.Instance.Ammo)
            {
                MinionManager.FarmLocation bestPositionE =
                    E.GetCircularFarmLocation(
                        MinionManager.GetMinions(
                            ObjectManager.Player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.NotAlly),
                        BarrelExplosionRange);

                if (bestPositionE.MinionsHit >= config.Item("eMinHit", true).GetValue<Slider>().Value &&
                    bestPositionE.Position.Distance(ePos) > 400)
                {
                    E.Cast(bestPositionE.Position, config.Item("packets").GetValue<bool>());
                }
            }
        }

        private void Combo()
        {
            var target = TargetSelector.GetTarget(
                E.Range, TargetSelector.DamageType.Physical, true, HeroManager.Enemies.Where(h => h.IsInvulnerable));
            if (target == null)
            {
                return;
            }
            var ignitedmg = (float) player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);
            bool hasIgnite = player.Spellbook.CanUseSpell(player.GetSpellSlot("SummonerDot")) == SpellState.Ready;
            if (config.Item("useIgnite", true).GetValue<bool>() &&
                ignitedmg > target.Health - Program.IncDamages.GetEnemyData(target.NetworkId).DamageTaken && hasIgnite &&
                !CombatHelper.CheckCriticalBuffs(target) && !Q.IsReady() && !justQ)
            {
                player.Spellbook.CastSpell(player.GetSpellSlot("SummonerDot"), target);
            }
            if (config.Item("usew", true).GetValue<Slider>().Value > player.HealthPercent &&
                player.CountEnemiesInRange(500) > 0)
            {
                W.Cast();
            }
            if (R.IsReady() && config.Item("user", true).GetValue<bool>())
            {
                var Rtarget =
                    HeroManager.Enemies.FirstOrDefault(e => e.HealthPercent < 50 && e.CountAlliesInRange(660) > 0);
                if (Rtarget != null)
                {
                    R.CastIfWillHit(Rtarget, config.Item("Rmin", true).GetValue<Slider>().Value);
                }
            }
            if (config.Item("useItems").GetValue<bool>())
            {
                ItemHandler.UseItems(target, config, ComboDamage(target), config.Item("AutoW", true).GetValue<bool>());
            }
            var dontQ = false;
            var barrels =
                GetBarrels()
                    .Where(
                        o =>
                            o.IsValid && !o.IsDead && o.Distance(player) < 1600 && o.SkinName == "GangplankBarrel" &&
                            o.GetBuff("gangplankebarrellife").Caster.IsMe)
                    .ToList();

            if (config.Item("useq", true).GetValue<bool>() && Q.IsReady() && config.Item("usee", true).GetValue<bool>() &&
                E.IsReady() && Orbwalking.CanMove(100) && !justE)
            {
                var pred = Prediction.GetPrediction(target, 0.5f);
                if (pred.Hitchance >= HitChance.High)
                {
                    var Qbarrels = GetBarrels().Where(o => o.Distance(player) < Q.Range && KillableBarrel(o));
                    foreach (var Qbarrel in Qbarrels.OrderByDescending(b => b.Distance(target) < BarrelExplosionRange))
                    {
                        var targPred = Prediction.GetPrediction(target, GetQTime(Qbarrel));
                        if (Qbarrel.Distance(targPred.UnitPosition) < BarrelExplosionRange)
                        {
                            if (config.Item("useeAOE", true).GetValue<bool>() && barrels.Count < 2)
                            {
                                var enemies =
                                    HeroManager.Enemies.Where(
                                        e => e.Distance(player) < 1600 && e.Distance(Qbarrel) > BarrelExplosionRange)
                                        .Select(e => Prediction.GetPrediction(e, 05f));
                                var pos =
                                    GetBarrelPoints(Qbarrel.Position)
                                        .Where(p => p.Distance(Qbarrel.Position) < BarrelConnectionRange)
                                        .OrderByDescending(
                                            p => enemies.Count(e => e.UnitPosition.Distance(p) < BarrelExplosionRange))
                                        .ThenBy(p => p.Distance(target.Position))
                                        .FirstOrDefault();
                                if (pos.IsValid() && pos.CountEnemiesInRange(BarrelExplosionRange) > 0 &&
                                    enemies.Count(e => e.UnitPosition.Distance(pos) < BarrelExplosionRange) > 0)
                                {
                                    dontQ = true;
                                    E.Cast(pos);
                                }
                            }
                            break;
                        }
                        var point =
                            GetBarrelPoints(Qbarrel.Position)
                                .Where(
                                    p =>
                                        p.IsValid() && !p.IsWall() && p.Distance(player.Position) < E.Range &&
                                        target.Distance(p) < BarrelExplosionRange &&
                                        p.Distance(targPred.UnitPosition) < BarrelExplosionRange &&
                                        Qbarrel.Distance(p) < BarrelConnectionRange &&
                                        savedBarrels.Count(b => b.barrel.Position.Distance(p) < BarrelExplosionRange) <
                                        1)
                                .OrderBy(p => p.Distance(pred.UnitPosition))
                                .FirstOrDefault();
                        if (point.IsValid())
                        {
                            dontQ = true;
                            E.Cast(point);
                            Utility.DelayAction.Add(1, () => Q.CastOnUnit(Qbarrel));
                            return;
                        }
                    }
                }
            }
            var meleeRangeBarrel =
                barrels.FirstOrDefault(
                    b =>
                        (b.Health < 2 || (b.Health == 2 && !KillableBarrel(b, true) && Q.IsReady() && !justQ)) &&
                        KillableBarrel(b, true) && b.Distance(player) < Orbwalking.GetRealAutoAttackRange(b));
            var secondb =
                barrels.Where(
                    b =>
                        b.Distance(meleeRangeBarrel) < BarrelConnectionRange &&
                        HeroManager.Enemies.Count(
                            o =>
                                o.IsValidTarget() && o.Distance(b) < BarrelExplosionRange &&
                                b.Distance(Prediction.GetPrediction(o, 500).UnitPosition) < BarrelExplosionRange) > 0);
            if (meleeRangeBarrel != null &&
                ((HeroManager.Enemies.Count(
                    o =>
                        o.IsValidTarget() && o.Distance(meleeRangeBarrel) < BarrelExplosionRange &&
                        meleeRangeBarrel.Distance(Prediction.GetPrediction(o, 500).UnitPosition) < BarrelExplosionRange) >
                  0) || secondb != null) && !Q.IsReady() && !justQ && Orbwalking.CanAttack())
            {
                orbwalker.SetAttack(false);
                player.IssueOrder(GameObjectOrder.AttackUnit, meleeRangeBarrel);
            }
            if (Q.IsReady())
            {
                if (barrels.Any())
                {
                    var detoneateTargetBarrels = barrels.Where(b => b.Distance(player) < Q.Range);
                    if (config.Item("detoneateTarget", true).GetValue<bool>())
                    {
                        if (detoneateTargetBarrels.Any())
                        {
                            foreach (var detoneateTargetBarrel in detoneateTargetBarrels)
                            {
                                if (!KillableBarrel(detoneateTargetBarrel))
                                {
                                    continue;
                                }
                                if (
                                    detoneateTargetBarrel.Distance(
                                        Prediction.GetPrediction(target, GetQTime(detoneateTargetBarrel)).UnitPosition) <
                                    BarrelExplosionRange &&
                                    target.Distance(detoneateTargetBarrel.Position) < BarrelExplosionRange)
                                {
                                    dontQ = true;
                                    Q.CastOnUnit(detoneateTargetBarrel, config.Item("packets").GetValue<bool>());
                                    return;
                                }
                                var detoneateTargetBarrelSeconds =
                                    barrels.Where(b => b.Distance(detoneateTargetBarrel) < BarrelConnectionRange);
                                if (detoneateTargetBarrelSeconds.Any())
                                {
                                    foreach (var detoneateTargetBarrelSecond in detoneateTargetBarrelSeconds)
                                    {
                                        if (
                                            detoneateTargetBarrelSecond.Distance(
                                                Prediction.GetPrediction(
                                                    target, GetQTime(detoneateTargetBarrel) + 0.15f).UnitPosition) <
                                            BarrelExplosionRange &&
                                            target.Distance(detoneateTargetBarrelSecond.Position) < BarrelExplosionRange)
                                        {
                                            dontQ = true;
                                            Q.CastOnUnit(detoneateTargetBarrel, config.Item("packets").GetValue<bool>());
                                            return;
                                        }
                                    }
                                }
                            }
                        }

                        if (config.Item("detoneateTargets", true).GetValue<Slider>().Value > 1)
                        {
                            var enemies =
                                HeroManager.Enemies.Where(e => e.IsValidTarget() && e.Distance(player) < 600)
                                    .Select(e => Prediction.GetPrediction(e, 0.25f));
                            var enemies2 =
                                HeroManager.Enemies.Where(e => e.IsValidTarget() && e.Distance(player) < 600)
                                    .Select(e => Prediction.GetPrediction(e, 0.35f));
                            if (detoneateTargetBarrels.Any())
                            {
                                foreach (var detoneateTargetBarrel in detoneateTargetBarrels)
                                {
                                    if (!KillableBarrel(detoneateTargetBarrel))
                                    {
                                        continue;
                                    }
                                    var enemyCount =
                                        enemies.Count(
                                            e =>
                                                e.UnitPosition.Distance(detoneateTargetBarrel.Position) <
                                                BarrelExplosionRange);
                                    if (enemyCount >= config.Item("detoneateTargets", true).GetValue<Slider>().Value &&
                                        detoneateTargetBarrel.CountEnemiesInRange(BarrelExplosionRange) >=
                                        config.Item("detoneateTargets", true).GetValue<Slider>().Value)
                                    {
                                        dontQ = true;
                                        Q.CastOnUnit(detoneateTargetBarrel, config.Item("packets").GetValue<bool>());
                                        return;
                                    }
                                    var detoneateTargetBarrelSeconds =
                                        barrels.Where(b => b.Distance(detoneateTargetBarrel) < BarrelConnectionRange);
                                    if (detoneateTargetBarrelSeconds.Any())
                                    {
                                        foreach (var detoneateTargetBarrelSecond in detoneateTargetBarrelSeconds)
                                        {
                                            if (enemyCount +
                                                enemies2.Count(
                                                    e =>
                                                        e.UnitPosition.Distance(detoneateTargetBarrelSecond.Position) <
                                                        BarrelExplosionRange) >=
                                                config.Item("detoneateTargets", true).GetValue<Slider>().Value &&
                                                detoneateTargetBarrelSecond.CountEnemiesInRange(BarrelExplosionRange) >=
                                                config.Item("detoneateTargets", true).GetValue<Slider>().Value)
                                            {
                                                dontQ = true;
                                                Q.CastOnUnit(
                                                    detoneateTargetBarrel, config.Item("packets").GetValue<bool>());
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (config.Item("useeAlways", true).GetValue<bool>() && E.IsReady() && player.Distance(target) < E.Range &&
                    !justE && target.Health > Q.GetDamage(target) + player.GetAutoAttackDamage(target) &&
                    Orbwalking.CanMove(100) && config.Item("eStacksC", true).GetValue<Slider>().Value < E.Instance.Ammo)
                {
                    CastE(target, barrels);
                }
                var Qbarrels = GetBarrels().FirstOrDefault(o => o.Distance(player) < Q.Range);
                if (Qbarrels != null && E.Instance.Ammo > 0 && Q.IsReady() && config.Item("usee", true).GetValue<bool>() &&
                    target.Health > Q.GetDamage(target))
                {
                    dontQ = true;
                }
                if (config.Item("useq", true).GetValue<bool>() && Q.CanCast(target) && Orbwalking.CanMove(100) && !justE &&
                    !dontQ)
                {
                    CastQonHero(target, barrels);
                }
            }
        }

        private void CastQonHero(Obj_AI_Hero target, List<Obj_AI_Minion> barrels)
        {
            if (barrels.FirstOrDefault(b => target.Distance(b.Position) < BarrelExplosionRange) != null &&
                target.Health > Q.GetDamage(target))
            {
                return;
            }
            Q.CastOnUnit(target, config.Item("packets").GetValue<bool>());
        }

        private void CastE(Obj_AI_Hero target, List<Obj_AI_Minion> barrels)
        {
            if (barrels.Count(b => b.CountEnemiesInRange(BarrelConnectionRange) > 0) < 1)
            {
                if (config.Item("useeAlways", true).GetValue<bool>())
                {
                    CastEtarget(target);
                }
                return;
            }
            var enemies =
                HeroManager.Enemies.Where(e => e.IsValidTarget() && e.Distance(player) < E.Range)
                    .Select(e => Prediction.GetPrediction(e, 0.35f));
            List<Vector3> points = new List<Vector3>();
            foreach (var barrel in
                barrels.Where(b => b.Distance(player) < Q.Range && KillableBarrel(b)))
            {
                if (barrel != null)
                {
                    var newP = GetBarrelPoints(barrel.Position).Where(p => !p.IsWall());
                    if (newP.Any())
                    {
                        points.AddRange(newP.Where(p => p.Distance(player.Position) < E.Range));
                    }
                }
            }
            var bestPoint =
                points.Where(b => enemies.Count(e => e.UnitPosition.Distance(b) < BarrelExplosionRange) > 0)
                    .OrderByDescending(b => enemies.Count(e => e.UnitPosition.Distance(b) < BarrelExplosionRange))
                    .FirstOrDefault();
            if (bestPoint.IsValid() &&
                !savedBarrels.Any(b => b.barrel.Position.Distance(bestPoint) < BarrelConnectionRange))
            {
                E.Cast(bestPoint, config.Item("packets").GetValue<bool>());
            }
        }

        private void CastEtarget(Obj_AI_Hero target)
        {
            var ePred = Prediction.GetPrediction(target, 1);
            var pos = target.Position.Extend(ePred.CastPosition, BarrelExplosionRange);
            if (pos.Distance(ePos) > 400 && !justE)
            {
                E.Cast(pos, config.Item("packets").GetValue<bool>());
            }
        }

        private void Game_OnDraw(EventArgs args)
        {
            DrawHelper.DrawCircle(config.Item("drawqq", true).GetValue<Circle>(), Q.Range);
            DrawHelper.DrawCircle(config.Item("drawee", true).GetValue<Circle>(), E.Range);
            var drawecr = config.Item("draweecr", true).GetValue<Circle>();
            if (drawecr.Active)
            {
                foreach (var barrel in GetBarrels().Where(b => b.Distance(player) < E.Range + BarrelConnectionRange))
                {
                    Render.Circle.DrawCircle(barrel.Position, BarrelConnectionRange, drawecr.Color, 7);
                }
            }
            Helpers.Jungle.ShowSmiteStatus(
                config.Item("useSmite").GetValue<KeyBind>().Active, config.Item("smiteStatus").GetValue<bool>());
            Utility.HpBarDamageIndicator.Enabled = config.Item("drawcombo", true).GetValue<bool>();
            if (config.Item("drawW", true).GetValue<bool>())
            {
                if (W.IsReady() && player.HealthPercent < 100)
                {
                    float Heal = new int[] { 50, 75, 100, 125, 150 }[W.Level - 1] +
                                 (player.MaxHealth - player.Health) * 0.15f + player.FlatMagicDamageMod * 0.9f;
                    float mod = Math.Max(100f, player.Health + Heal) / player.MaxHealth;
                    float xPos = (float) ((double) player.HPBarPosition.X + 36 + 103.0 * mod);
                    Drawing.DrawLine(
                        xPos, player.HPBarPosition.Y + 8, xPos, (float) ((double) player.HPBarPosition.Y + 17), 2f,
                        Color.Coral);
                }
            }
            if (config.Item("drawKillableSL", true).GetValue<StringList>().SelectedIndex != 0 && R.IsReady())
            {
                var text = new List<string>();
                foreach (var enemy in HeroManager.Enemies.Where(e => e.IsValidTarget()))
                {
                    if (getRDamage(enemy) > enemy.Health)
                    {
                        text.Add(enemy.ChampionName + "(" + Math.Ceiling(enemy.Health / Rwave[R.Level - 1]) + " wave)");
                    }
                }
                if (text.Count > 0)
                {
                    var result = string.Join(", ", text);
                    switch (config.Item("drawKillableSL", true).GetValue<StringList>().SelectedIndex)
                    {
                        case 2:
                            drawText(2, result);
                            break;
                        case 1:
                            drawText(1, result);
                            break;
                        default:
                            return;
                    }
                }
            }

            if (Q.IsReady() && config.Item("drawEQ", true).GetValue<bool>())
            {
                var points =
                    CombatHelper.PointsAroundTheTarget(player.Position, E.Range - 200, 15, 6)
                        .Where(p => p.Distance(player.Position) < E.Range);


                var barrel =
                    GetBarrels()
                        .Where(
                            o =>
                                o.IsValid && !o.IsDead && o.Distance(player) < Q.Range &&
                                o.SkinName == "GangplankBarrel" && o.GetBuff("gangplankebarrellife").Caster.IsMe &&
                                KillableBarrel(o))
                        .OrderBy(o => o.Distance(Game.CursorPos))
                        .FirstOrDefault();
                if (barrel != null)
                {
                    var cp = Game.CursorPos;
                    var cursorPos = barrel.Distance(cp) > BarrelConnectionRange
                        ? barrel.Position.Extend(cp, BarrelConnectionRange)
                        : cp;
                    var cursorPos2 = cursorPos.Distance(cp) > BarrelConnectionRange
                        ? cursorPos.Extend(cp, BarrelConnectionRange)
                        : cp;
                    var middle = GetMiddleBarrel(barrel, points, cursorPos);
                    var threeBarrel = cursorPos.Distance(cp) > BarrelExplosionRange && E.Instance.Ammo >= 2 &&
                                      cursorPos2.Distance(player.Position) < E.Range && middle.IsValid();
                    if (threeBarrel)
                    {
                        Render.Circle.DrawCircle(
                            middle.Extend(cp, BarrelConnectionRange), BarrelExplosionRange, Color.DarkOrange, 6);
                        Render.Circle.DrawCircle(middle, BarrelExplosionRange, Color.DarkOrange, 6);
                        Drawing.DrawLine(
                            Drawing.WorldToScreen(barrel.Position),
                            Drawing.WorldToScreen(middle.Extend(barrel.Position, BarrelExplosionRange)), 2,
                            Color.DarkOrange);
                    }
                    else if (E.Instance.Ammo >= 1)
                    {
                        Drawing.DrawLine(
                            Drawing.WorldToScreen(barrel.Position),
                            Drawing.WorldToScreen(cursorPos.Extend(barrel.Position, BarrelExplosionRange)), 2,
                            Color.DarkOrange);
                        Render.Circle.DrawCircle(cursorPos, BarrelExplosionRange, Color.DarkOrange, 6);
                    }
                }
            }
            if (config.Item("drawWcd", true).GetValue<bool>())
            {
                foreach (var barrelData in savedBarrels)
                {
                    float time =
                        Math.Min(
                            System.Environment.TickCount - barrelData.time -
                            barrelData.barrel.Health * getEActivationDelay() * 1000f, 0) / 1000f;
                    if (time < 0)
                    {
                        Drawing.DrawText(
                            barrelData.barrel.HPBarPosition.X - time.ToString().Length * 5 + 40,
                            barrelData.barrel.HPBarPosition.Y - 20, Color.DarkOrange,
                            string.Format("{0:0.00}", time).Replace("-", ""));
                    }
                }
            }

            if (config.Item("drawEmini", true).GetValue<bool>())
            {
                var barrels =
                    GetBarrels()
                        .Where(
                            o =>
                                o.IsValid && !o.IsDead && o.Distance(player) < E.Range &&
                                o.SkinName == "GangplankBarrel" && o.GetBuff("gangplankebarrellife").Caster.IsMe);
                foreach (var b in barrels)
                {
                    var minis = MinionManager.GetMinions(
                        b.Position, BarrelExplosionRange, MinionTypes.All, MinionTeam.NotAlly);
                    foreach (var m in
                        minis.Where(e => Q.GetDamage(e) + ItemHandler.GetSheenDmg(e) >= e.Health && e.Health > 3))
                    {
                        Render.Circle.DrawCircle(m.Position, 45, Color.Yellow, 7);
                    }
                }
            }
        }

        public void drawText(int mode, string result)
        {
            const string baseText = "Killable with R: ";
            if (mode == 1)
            {
                Drawing.DrawText(
                    Drawing.Width / 2 - (baseText + result).Length * 5, Drawing.Height * 0.75f, Color.Red,
                    baseText + result);
            }
            else
            {
                Drawing.DrawText(
                    player.HPBarPosition.X - (baseText + result).Length * 5 + 110, player.HPBarPosition.Y + 250,
                    Color.Red, baseText + result);
            }
        }

        private float getRDamage(Obj_AI_Hero enemy)
        {
            return
                (float)
                    Damage.CalcDamage(
                        player, enemy, Damage.DamageType.Magical,
                        (Rwave[R.Level - 1] + 0.1 * player.FlatMagicDamageMod) * waveLength());
        }

        public int waveLength()
        {
            if (player.HasBuff("GangplankRUpgrade1"))
            {
                return 18;
            }
            else
            {
                return 12;
            }
        }

        private static float ComboDamage(Obj_AI_Hero hero)
        {
            double damage = 0;
            if (Q.IsReady())
            {
                damage += Damage.GetSpellDamage(player, hero, SpellSlot.Q);
            }
            //damage += ItemHandler.GetItemsDamage(hero);
            var ignitedmg = player.GetSummonerSpellDamage(hero, Damage.SummonerSpell.Ignite);
            if (player.Spellbook.CanUseSpell(player.GetSpellSlot("summonerdot")) == SpellState.Ready &&
                hero.Health < damage + ignitedmg)
            {
                damage += ignitedmg;
            }
            return (float) damage;
        }

        private void Game_ProcessSpell(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe)
            {
                if (args.SData.Name == "GangplankQWrapper")
                {
                    if (!justQ)
                    {
                        justQ = true;
                        Utility.DelayAction.Add(200, () => justQ = false);
                    }
                }
                if (args.SData.Name == "GangplankE")
                {
                    ePos = args.End;
                    if (!justE)
                    {
                        justE = true;
                        Utility.DelayAction.Add(500, () => justE = false);
                    }
                }
            }
            if (sender.IsEnemy && args.Target != null && sender is Obj_AI_Hero && sender.Distance(player) < E.Range)
            {
                var targetBarrels =
                    savedBarrels.Where(
                        b =>
                            b.barrel.NetworkId == args.Target.NetworkId &&
                            KillableBarrel(
                                b.barrel, sender.IsMelee, (Obj_AI_Hero) sender,
                                sender.Distance(b.barrel) / args.SData.MissileSpeed));
                foreach (var barrelData in targetBarrels)
                {
                    savedBarrels.Remove(barrelData);
                    Console.WriteLine("Barrel removed");
                }
            }
        }

        private IEnumerable<Vector3> GetBarrelPoints(Vector3 point)
        {
            return
                CombatHelper.PointsAroundTheTarget(point, BarrelConnectionRange, 20f)
                    .Where(p => p.Distance(point) > BarrelExplosionRange);
        }

        private float getEActivationDelay()
        {
            if (player.Level >= 13)
            {
                return 0.5f;
            }
            if (player.Level >= 7)
            {
                return 1f;
            }
            return 2f;
        }

        private void InitMenu()
        {
            config = new Menu("Gangplank ", "Gangplank", true);
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
                .SetValue(new Circle(false, Color.FromArgb(180, 100, 146, 166)));
            menuD.AddItem(new MenuItem("drawW", "Draw W", true)).SetValue(true);
            menuD.AddItem(new MenuItem("drawee", "Draw E range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 100, 146, 166)));
            menuD.AddItem(new MenuItem("draweecr", "Draw Connection ranges", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 167, 141, 56)));
            menuD.AddItem(new MenuItem("drawWcd", "Draw E countdown", true)).SetValue(true);
            menuD.AddItem(new MenuItem("drawEmini", "Draw killable minions around E", true)).SetValue(true);
            menuD.AddItem(new MenuItem("drawcombo", "Draw combo damage", true)).SetValue(true);
            menuD.AddItem(new MenuItem("drawEQ", "Draw EQ to cursor", true)).SetValue(true);
            menuD.AddItem(new MenuItem("drawKillableSL", "Show killable targets with R", true))
                .SetValue(new StringList(new[] { "OFF", "Above HUD", "Under GP" }, 1));
            config.AddSubMenu(menuD);
            // Combo Settings
            Menu menuC = new Menu("Combo ", "csettings");
            menuC.AddItem(new MenuItem("useq", "Use Q", true)).SetValue(true);
            menuC.AddItem(new MenuItem("detoneateTarget", "   Blow up target with E", true)).SetValue(true);
            menuC.AddItem(new MenuItem("detoneateTargets", "   Blow up enemies with E", true))
                .SetValue(new Slider(2, 1, 5));
            menuC.AddItem(new MenuItem("usew", "Use W under health", true)).SetValue(new Slider(20, 0, 100));
            menuC.AddItem(new MenuItem("useeAlways", "Use E always", true))
                .SetTooltip(
                    "NOT RECOMMENDED, If there is no barrel around the target, this will put one to a predicted position")
                .SetValue(false);
            menuC.AddItem(new MenuItem("eStacksC", "   Keep stacks", true))
                .SetTooltip("You can set up how many barrels you want to keep to \"Use E to extend range\"")
                .SetValue(new Slider(0, 0, 5));
            menuC.AddItem(new MenuItem("usee", "Use E to extend range", true))
                .SetTooltip(
                    "If there is a barrel, but the target is out of its range, this will put a new barrel around the target, and cast a Q on the first")
                .SetValue(true);
            menuC.AddItem(new MenuItem("useeAOE", "   Put bonus barrel to AOE", true))
                .SetTooltip(
                    "If the target is near to a barrel, not necessary to put more, but this will put ONE to increase AOE damage")
                .SetValue(false);
            menuC.AddItem(new MenuItem("EQtoCursor", "EQ to cursor", true))
                .SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press))
                .SetFontStyle(System.Drawing.FontStyle.Bold, SharpDX.Color.Orange);
            menuC.AddItem(new MenuItem("QbarrelCursor", "Q barrel at cursor", true))
                .SetValue(new KeyBind("H".ToCharArray()[0], KeyBindType.Press))
                .SetFontStyle(System.Drawing.FontStyle.Bold, SharpDX.Color.Orange);
            menuC.AddItem(new MenuItem("user", "Use R", true)).SetValue(true);
            menuC.AddItem(new MenuItem("Rmin", "   R min", true)).SetValue(new Slider(2, 1, 5));
            menuC.AddItem(new MenuItem("useIgnite", "Use Ignite", true)).SetValue(true);
            menuC = ItemHandler.addItemOptons(menuC);
            config.AddSubMenu(menuC);
            // Harass Settings
            Menu menuH = new Menu("Harass ", "Hsettings");
            menuH.AddItem(new MenuItem("useqH", "Use Q harass", true)).SetValue(true);
            menuH.AddItem(new MenuItem("useqLHH", "Use Q lasthit", true)).SetValue(true);
            menuH.AddItem(new MenuItem("useeH", "Use E", true)).SetValue(true);
            menuH.AddItem(new MenuItem("eStacksH", "   Keep stacks", true)).SetValue(new Slider(0, 0, 5));
            menuH.AddItem(new MenuItem("minmanaH", "Keep X% mana", true)).SetValue(new Slider(1, 1, 100));
            config.AddSubMenu(menuH);
            // LaneClear Settings
            Menu menuLC = new Menu("LaneClear ", "Lcsettings");
            menuLC.AddItem(new MenuItem("useqLC", "Use Q", true)).SetValue(true);
            menuLC.AddItem(new MenuItem("useeLC", "Use E", true)).SetValue(true);
            menuLC.AddItem(new MenuItem("eMinHit", "   Min hit", true)).SetValue(new Slider(3, 1, 6));
            menuLC.AddItem(new MenuItem("eStacksLC", "   Keep stacks", true)).SetValue(new Slider(0, 0, 5));
            menuLC.AddItem(new MenuItem("minmana", "Keep X% mana", true)).SetValue(new Slider(1, 1, 100));
            config.AddSubMenu(menuLC);
            Menu menuM = new Menu("Misc ", "Msettings");
            menuM.AddItem(new MenuItem("AutoR", "Cast R to get assists", true)).SetValue(false);
            menuM.AddItem(new MenuItem("Rhealt", "   Enemy health %", true))
                .SetTooltip("Max enemy healthpercent to cast R")
                .SetValue(new Slider(35, 0, 100));
            menuM.AddItem(new MenuItem("RhealtMin", "   Enemy min health %", true))
                .SetTooltip("min enemy healthpercent to prevent KS or casting too late")
                .SetValue(new Slider(10, 0, 100));
            menuM.AddItem(new MenuItem("AutoW", "W with QSS options", true)).SetValue(true);
            menuM.AddItem(new MenuItem("AutoQBarrel", "AutoQ barrel near enemies", true)).SetValue(false);
            menuM = Jungle.addJungleOptions(menuM);

            Menu autolvlM = new Menu("AutoLevel", "AutoLevel");
            autoLeveler = new AutoLeveler(autolvlM);
            menuM.AddSubMenu(autolvlM);
            config.AddSubMenu(menuM);
            config.AddItem(new MenuItem("packets", "Use Packets")).SetValue(false);
            config.AddItem(new MenuItem("UnderratedAIO", "by Soresu v" + Program.version.ToString().Replace(",", ".")));
            config.AddToMainMenu();
        }
    }

    internal class Barrel
    {
        public Obj_AI_Minion barrel;
        public float time;

        public Barrel(Obj_AI_Minion objAiBase, int tickCount)
        {
            barrel = objAiBase;
            time = tickCount;
        }
    }
}
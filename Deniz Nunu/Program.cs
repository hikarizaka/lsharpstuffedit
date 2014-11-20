#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;

#endregion

namespace Nunu
{
    internal class Program
    {
        public const string ChampionName = "Nunu";

        //Orbwalker instance
        public static Orbwalking.Orbwalker Orbwalker;

        //Spells
        public static List<Spell> SpellList = new List<Spell>();

        public static Spell Q;
        public static Spell W;
        public static Spell E;
        public static Spell R;
        private static bool packetCast;
        //Menu
        public static Menu Config;

        private static Obj_AI_Hero Player;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            Player = ObjectManager.Player;
            if (Player.BaseSkinName != ChampionName) return;

            //Create the spells
            Q = new Spell(SpellSlot.Q, 125);
            W = new Spell(SpellSlot.W, 700);
            E = new Spell(SpellSlot.E, 550);
            E = new Spell(SpellSlot.R, 650);
            SpellList.Add(Q);
            SpellList.Add(W);
            SpellList.Add(E);

            //Create the menu
            Config = new Menu(ChampionName, ChampionName, true);

            var targetSelectorMenu = new Menu("Target Selector", "Target Selector");
            SimpleTs.AddToMenu(targetSelectorMenu);
            Config.AddSubMenu(targetSelectorMenu);

            //Orbwalker submenu
            Config.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));

            //Load the orbwalker and add it to the submenu.
            Orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalking"));

            Config.AddSubMenu(new Menu("Combo", "Combo"));
            Config.SubMenu("Combo").AddItem(new MenuItem("ComboR","Use R?").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("MinEnemys", "Min enemys for R")).SetValue(new Slider(3, 5, 1));
            Config.SubMenu("Combo").AddItem(new MenuItem("ComboActive", "Combo!").SetValue(new KeyBind(32, KeyBindType.Press)));

            Config.AddSubMenu(new Menu("Harass", "Harass"));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseEHarass", "Use E").SetValue(true));
            Config.SubMenu("Harass").AddItem(new MenuItem("EMana", "Min Mana E").SetValue(new Slider(40, 1, 100)));
            Config.SubMenu("Harass").AddItem(new MenuItem("HarassActive", "Harass!").SetValue(new KeyBind("C".ToCharArray()[0], KeyBindType.Press)));

            Config.AddSubMenu(new Menu("Farm", "Farm"));
            Config.SubMenu("Farm").AddItem(new MenuItem("UseQFarm", "Use Q").SetValue(true));
            
            Config.SubMenu("Farm").AddItem(new MenuItem("FreezeActive", "Freeze!").SetValue(new KeyBind("C".ToCharArray()[0], KeyBindType.Press)));

     

            //Damage after combo:
            var eDamage = new MenuItem("DamageAfterCombo", "Draw Damage After Used E").SetValue(true);
            Utility.HpBarDamageIndicator.DamageToUnit += hero => (float)(Player.GetSpellDamage(hero, SpellSlot.E)); 
            Utility.HpBarDamageIndicator.Enabled = eDamage.GetValue<bool>();
            eDamage.ValueChanged += delegate(object sender, OnValueChangeEventArgs eventArgs)
            {
                Utility.HpBarDamageIndicator.Enabled = eventArgs.GetNewValue<bool>();
            };

            Config.AddSubMenu(new Menu("KS", "KS"));
            Config.SubMenu("KS").AddItem(new MenuItem("StealE","Steal With E").SetValue(true));
            Config.SubMenu("KS").AddItem(new MenuItem("BreakU","Break Ulti To Steal?").SetValue(false));
            Config.AddSubMenu(new Menu("Misc", "Misc"));
            Config.SubMenu("Misc").AddItem(new MenuItem("HealQ", "Use Q if HP is not Full").SetValue(true));
            Config.SubMenu("Misc").AddItem(new MenuItem("QMana", "Min Mana Q").SetValue(new Slider(40, 1, 100)));
            Config.SubMenu("Misc").AddItem(new MenuItem("GetAsisted", "Use W On Allies").SetValue(true));
            Config.SubMenu("Misc").AddItem(new MenuItem("WMana", "Min Mana W").SetValue(new Slider(40, 1, 100)));
            Config.SubMenu("Misc").AddItem(new MenuItem("Harasser", "Harass Allways").SetValue(true));
            Config.AddItem(new MenuItem("packetCast", "Packet Cast").SetValue(true));
            Config.AddSubMenu(new Menu("Drawings", "Drawings"));
            Config.SubMenu("Drawings")
                .AddItem(new MenuItem("QRange", "Q range").SetValue(new Circle(true, Color.FromArgb(255, 255, 255, 255))));
            Config.SubMenu("Drawings")
                .AddItem(
                    new MenuItem("WRange", "W range").SetValue(new Circle(false, Color.FromArgb(255, 255, 255, 255))));
            Config.SubMenu("Drawings")
                .AddItem(
                    new MenuItem("ERange", "E range").SetValue(new Circle(false, Color.FromArgb(255, 255, 255, 255))));
            Config.SubMenu("Drawings").AddItem(eDamage);
            Config.AddToMainMenu();

            //Add the events we are going to use:
            Drawing.OnDraw += Drawing_OnDraw;
            Game.OnGameUpdate += Game_OnGameUpdate;
            Orbwalking.BeforeAttack += OrbwalkingOnBeforeAttack;
        }

        private static void OrbwalkingOnBeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            if (Config.Item("ComboActive").GetValue<KeyBind>().Active)
                args.Process = !(Q.IsReady() || W.IsReady() || E.IsReady() || Player.Distance(args.Target) >= 600);
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            //Draw the ranges of the spells.
            foreach (var spell in SpellList)
            {
                var menuItem = Config.Item(spell.Slot + "Range").GetValue<Circle>();
                if (menuItem.Active)
                {
                    Utility.DrawCircle(Player.Position, spell.Range, menuItem.Color);
                }
            }
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            packetCast = Config.Item("UsePacket").GetValue<bool>();
            if (Config.Item("ComboActive").GetValue<KeyBind>().Active)
            {
                Combo();
            }
            else
            {
                if (Config.Item("HarassActive").GetValue<KeyBind>().Active)
                    Harass();

                
                if (Config.Item("FreezeActive").GetValue<KeyBind>().Active)
                    Farm();
            }



            //Misc -
            if (Config.Item("StealE").GetValue<bool>())
            {
                var target = SimpleTs.GetTarget(E.Range, SimpleTs.DamageType.Magical); 
                if (target.IsValidTarget(E.Range) && target.Health < Player.GetSpellDamage(Player, SpellSlot.E))
                {
                if (Config.Item("BreakU").GetValue<bool>())
                {
                    E.CastOnUnit(target, packetCast);
                }
                if (target != null && Config.Item("UseEHarass").GetValue<bool>() && !(Player.IsChannelingImportantSpell()))
                {
                    E.CastOnUnit(target, packetCast);
                }
                }
            }

            if (Config.Item("Harasser").GetValue<bool>())
            {
                var target = SimpleTs.GetTarget(E.Range, SimpleTs.DamageType.Magical); 
                var ManaHarass = Config.Item("EMana").GetValue<Slider>().Value;
                if (target != null && Config.Item("UseEHarass").GetValue<bool>() && (Player.Mana / Player.MaxMana * 100) > ManaHarass && !(Player.IsChannelingImportantSpell()))
                {
                    E.CastOnUnit(target, packetCast);
                }
            }
            if(Config.Item("HealQ").GetValue<bool>())
            {

                var ManaQMinion = Config.Item("QMana").GetValue<Slider>().Value;
                var allMinions = MinionManager.GetMinions(ObjectManager.Player.Position, Q.Range, MinionTypes.All);
                if (Q.IsReady() && ((Player.Health / Player.MaxHealth * 100) < 99) && (Player.Mana/Player.MaxMana*100) > ManaQMinion && !(Player.IsChannelingImportantSpell()))
                {
                    foreach (var minion in allMinions)
                    {
                        Q.CastOnUnit(minion, packetCast);
                    }
                }
            }
            if(Config.Item("GetAsisted").GetValue<bool>())
            {
                var ManaW = Config.Item("WMana").GetValue<Slider>().Value;
                if (W.IsReady() && (Player.Mana / Player.MaxMana * 100) > ManaW && !(Player.IsChannelingImportantSpell()))
                {
                    W.CastOnUnit(FriendlyTarget());
                }
            }
        }

        private static void Combo()
        {
            var target = SimpleTs.GetTarget(Q.Range, SimpleTs.DamageType.Magical);
            if (target != null)
            {
                   
                    if (Player.Distance(target) >= 550 && E.IsReady() && !(Player.IsChannelingImportantSpell()))// snow ball
                    {
                        E.CastOnUnit(target, packetCast);
                    }
                    if (W.IsReady() && Player.Distance(target) >= 130 && !(Player.IsChannelingImportantSpell()))
                    {
                        W.Cast(Player);
                    }
                    if (Config.Item("ComboR").GetValue<bool>() && Player.Distance(target) >= 540 && R.IsReady())
                     {
                     if(GetEnemys(target) >= Config.Item("MinEnemys").GetValue<Slider>().Value)
                     {
                         R.Cast();
                     }
                     }
                }
               
            }
        
        public static Obj_AI_Hero FriendlyTarget()
        {
            Obj_AI_Hero target = null;
            var allyList = from hero in ObjectManager.Get<Obj_AI_Hero>()
                           where hero.IsAlly && hero.IsValidTarget(695, false)
                           select hero;

            foreach (Obj_AI_Hero xe in allyList.OrderByDescending(xe => xe.Health / xe.MaxHealth * 100))
            {
                target = xe;
            }

            return target;
        }
        private static int GetEnemys(Obj_AI_Hero target)
        {
            var Enemys = ObjectManager.Get<Obj_AI_Hero>().Where(en => en.Team != Player.Team && !en.IsDead && en.Distance(Player.Position) < W.Range && en.IsValid);
            return Enemys.Count();
         }
        private static void Harass()
        {
            var target = SimpleTs.GetTarget(E.Range, SimpleTs.DamageType.Magical);
            var ManaHarass = Config.Item("EMana").GetValue<Slider>().Value;
            if (E.IsReady())
            {
                if (target != null && Config.Item("UseEHarass").GetValue<bool>() && (Player.Mana / Player.MaxMana * 100) > ManaHarass && !(Player.IsChannelingImportantSpell()))
            {

                E.CastOnUnit(target, packetCast);
            }
            }
        }

        private static void Farm()
        {
            if (!Orbwalking.CanMove(40)) return;
            var allMinions = MinionManager.GetMinions(Player.ServerPosition, Q.Range);
            if (Q.IsReady() && !(Player.IsChannelingImportantSpell()))
            {
                foreach (var minion in allMinions)
                {
                    if (minion.IsValidTarget() &&
                        HealthPrediction.GetHealthPrediction(minion,
                            (int)(Player.Distance(minion) * 1000 / 1400)) <
                         Player.GetSpellDamage(minion, SpellSlot.Q))
                    {
                        Q.CastOnUnit(minion);
                        return;
                    }
                }
            }
           

        }

    }
}
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

namespace Kalista
{
    internal class Program
    {
        public const string ChampionName = "Kalista";

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

            Q = new Spell(SpellSlot.Q, 1450);
            W = new Spell(SpellSlot.W, 5500);
            E = new Spell(SpellSlot.E, 950);
            R = new Spell(SpellSlot.R, 1250);
            Q.SetSkillshot(0.25f, 60f, 2000f, true, SkillshotType.SkillshotLine);
            W.SetSkillshot(0.25f, 80f, 1600f, false, SkillshotType.SkillshotLine);
            R.SetSkillshot(1f, 160f, 2000f, false, SkillshotType.SkillshotLine);
        
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
            Config.SubMenu("Combo").AddItem(new MenuItem("ComboQ", "Use Q?").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("ComboR","Use R?").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("ComboActive", "Combo!").SetValue(new KeyBind(32, KeyBindType.Press)));

            Config.AddSubMenu(new Menu("Harass", "Harass"));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseQHarass", "Use Q").SetValue(true));
            Config.SubMenu("Harass").AddItem(new MenuItem("QMana", "Min Mana Q").SetValue(new Slider(40, 1, 100)));
            Config.SubMenu("Harass").AddItem(new MenuItem("HarassActive", "Harass!").SetValue(new KeyBind("C".ToCharArray()[0], KeyBindType.Press)));

     

            //Damage after combo:
            var eDamage = new MenuItem("DamageAfterCombo", "Draw Damage After Used Q+2AA").SetValue(true);
            Utility.HpBarDamageIndicator.DamageToUnit += hero => (float)(Player.GetSpellDamage(hero, SpellSlot.Q) + (Player.GetAutoAttackDamage(hero)*2)); 
            Utility.HpBarDamageIndicator.Enabled = eDamage.GetValue<bool>();
            eDamage.ValueChanged += delegate(object sender, OnValueChangeEventArgs eventArgs)
            {
                Utility.HpBarDamageIndicator.Enabled = eventArgs.GetNewValue<bool>();
            };

            Config.AddSubMenu(new Menu("KS", "KS"));
            Config.SubMenu("KS").AddItem(new MenuItem("StealE","Steal With E").SetValue(true));
            Config.AddSubMenu(new Menu("Misc", "Misc"));
            Config.SubMenu("Misc").AddItem(new MenuItem("eStacks", "E Stacks").SetValue(new Slider(10, 2, 30)));
            Config.SubMenu("Misc").AddItem(new MenuItem("eMana", "Min Mana E").SetValue(new Slider(40, 1, 100)));
            Config.SubMenu("Misc").AddItem(new MenuItem("safer", "Use R to save allies").SetValue(true));
            Config.SubMenu("Misc").AddItem(new MenuItem("savehp", "HP to Save %").SetValue(new Slider(40, 1, 100)));
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
                      new MenuItem("RRange", "R range").SetValue(new Circle(false, Color.FromArgb(255, 255, 255, 255))));
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
                args.Process = !(Q.IsReady() || W.IsReady() || E.IsReady() || Player.Distance(args.Target) >= 550);
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

            }


           
            //Misc -
            if (E.IsReady())
            {
                var target = SimpleTs.GetTarget(Q.Range, SimpleTs.DamageType.Physical);
                var eman = Config.Item("EMana").GetValue<Slider>().Value;
                    if ((Player.Mana / Player.MaxMana * 100) > eman)
                        {
                            foreach (var buff in target.Buffs.Where(buff => buff.DisplayName.ToLower() == "kalistaexpungemarker").Where(buff => buff.Count == Config.Item("eStacks").GetValue<Slider>().Value))
                            {
                                 E.Cast(target);
                            }
                        }
            }
            if (Config.Item("StealE").GetValue<bool>())
            {
                var target = SimpleTs.GetTarget(Q.Range, SimpleTs.DamageType.Physical); 
                if (target.Health < Player.GetSpellDamage(Player, SpellSlot.E))
                {
                    E.Cast(target);
                }
            }

            if (Config.Item("Harasser").GetValue<bool>())
            {
                var target = SimpleTs.GetTarget(Q.Range, SimpleTs.DamageType.Physical); 
                var ManaHarass = Config.Item("QMana").GetValue<Slider>().Value;
                if (Q.IsReady() && target != null && Config.Item("UseQHarass").GetValue<bool>() && (Player.Mana / Player.MaxMana * 100) > ManaHarass && !(Player.IsChannelingImportantSpell()))
                {
                    Q.Cast(target);
                   
                }
            }
           
            if(Config.Item("safer").GetValue<bool>())
               {
                    R.CastOnUnit(FriendlyTarget());
               }
            }
        

        private static void Combo()
        {
            var target = SimpleTs.GetTarget(Q.Range, SimpleTs.DamageType.Physical);
            if (target != null)
            {

                if (Config.Item("ComboQ").GetValue<bool>() && Player.Distance(target) >= 1450 && Q.IsReady())// if target is in spear range and spear ready
                    {
                        Q.Cast(target);
                    }
                         if (E.IsReady())
                                {
                                    foreach (var buff in target.Buffs.Where(buff => buff.DisplayName.ToLower() == "kalistaexpungemarker").Where(buff => buff.Count == Config.Item("eStacks").GetValue<Slider>().Value))
                                    {
                                         E.Cast(target, packetCast);
                                    }
                                }

                                 if (Config.Item("ComboR").GetValue<bool>() && Player.Distance(target) >= 1200 && R.IsReady())
                                    {
                                         R.Cast(nearest());
                                    }
                }
               
            }
        public static Obj_AI_Hero nearest()
        {
            Obj_AI_Hero target = null;
            var allyList = from hero in ObjectManager.Get<Obj_AI_Hero>()
                           where hero.IsAlly && hero.IsValidTarget(1200, false)
                           select hero;

            foreach (Obj_AI_Hero xe in allyList.OrderByDescending(xe => xe.Position / Player.Position*100))
            {
                target = xe;
            }

            return target;
        }
        public static Obj_AI_Hero FriendlyTarget()
        {
            var hptosave = Config.Item("savehp").GetValue<Slider>().Value;
            Obj_AI_Hero target = null;
            var allyList = from hero in ObjectManager.Get<Obj_AI_Hero>()
                           where hero.IsAlly && hero.IsValidTarget(1200, false)
                           select hero;

            foreach (Obj_AI_Hero xe in allyList.OrderByDescending(xe => xe.Health / xe.MaxHealth * hptosave))
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
            var target = SimpleTs.GetTarget(Q.Range, SimpleTs.DamageType.Physical);
            var ManaHarass = Config.Item("QMana").GetValue<Slider>().Value;
            if (Q.IsReady())
            {
                if (target != null && Config.Item("UseQHarass").GetValue<bool>() && (Player.Mana / Player.MaxMana * 100) > ManaHarass && !(Player.IsChannelingImportantSpell()))
            {

                Q.Cast(target);
            }
            }
        }

    }
}
using System;
using System.Collections.Generic;
using System.Text;

namespace warlight
{
	class Planning
	{
		private static List<Plan> take_bonus()
		{
			List<Plan> plans = new List<Plan>();
			for(int bonus = 1; bonus < MapClass.bonuses.Count; ++bonus)
			{
				bool ok_plan = true;
				bool my_bonus = true;
				Plan p = new Plan();
				p.description = "take bonus " + bonus;
				p.bonus_id = bonus;
				List<int> regions_in_bonus = MapClass.bonuses[bonus].regions;
				foreach(int region_in_bonus in regions_in_bonus)			//check if every region in bonus is reachable
				{
					if(MapClass.regions[region_in_bonus].owner == OwnerEnum.Unknown)
					{
						ok_plan = false;
						break;
					}
					if((MapClass.regions[region_in_bonus].owner == OwnerEnum.Enemy) 
					   	|| (MapClass.regions[region_in_bonus].owner == OwnerEnum.Neutral))
					{
						my_bonus = false;
					}
				}
				
				if((!ok_plan) || (my_bonus))
					continue;

				List<Tuple<int, int>> attacks_locations = attacking_plans(regions_in_bonus);	//attacks sources and destinations
				List<double> success_by_units = new List<double>();
				List<Tuple<int, int>> free_armies = MapClass.existing_armies();
				List<Tuple<double, int, int>> action_description = new List<Tuple<double, int, int>>();	//success, attackers, defenders

				for(int i=0; i<attacks_locations.Count; ++i)
				{
					int free_armies_in_region = 0;
					int free_armies_index = -1;
					for(int j=0; j<free_armies.Count; ++j)		//find if there are any free armies in region from where I want attack
					{
						if(free_armies[j].Item1 == attacks_locations[i].Item1)
						{
							free_armies_in_region = free_armies[j].Item2;
							free_armies_index = j;
							break;
						}
					}
					
					int free_soldiers_to_use = 0;
					int enemy_soldiers = MapClass.regions[attacks_locations[i].Item2].number_of_armies;
					
					double probability = KnowledgeBase.attack_success(free_soldiers_to_use, enemy_soldiers);

					if(enemy_soldiers < 1)
						Utils.error_output("Error 13");

					while((probability < 0.5) && (free_soldiers_to_use < free_armies_in_region))	//decide how many free nuits will be used
						free_soldiers_to_use++;

					if(free_armies_index != -1)
					{
						//free_armies[free_armies_index].Item2 -= free_soldiers_to_use;
						free_armies[free_armies_index] = new Tuple<int, int>(free_armies[free_armies_index].Item1, free_armies[free_armies_index].Item2 - free_soldiers_to_use);
						p.needed_existing_armies.Add(new Tuple<int, int>(free_armies[free_armies_index].Item1, free_soldiers_to_use));
						if(free_armies[free_armies_index].Item2 == 0)
							free_armies.RemoveAt(free_armies_index);
					}

					Action a;
					a.existing_armies = free_soldiers_to_use;
					a.from = attacks_locations[i].Item1;
					a.to = attacks_locations[i].Item2;
					a.defend = false;
					p.actions.Add(a);
					action_description.Add(new Tuple<double, int, int>(probability, free_soldiers_to_use, MapClass.regions[a.to].number_of_armies));
				}

				for(int i=0; i<= Bot.armies_to_deploy; ++i)
				{
					
					double success = 1;
					int worst_index = 0;
					double worst_probability = 1;
					for(int j=0; j<action_description.Count; ++j)
					{
						success = success * action_description[j].Item1;
						if(action_description[j].Item1 < worst_probability)
						{
							worst_probability = action_description[j].Item1;
							worst_index = j;
						}
					}
						
					success_by_units.Add(success);

					p.units.Add(worst_index);

					int item2 = action_description[worst_index].Item2 + 1;
					int item3 = action_description[worst_index].Item3;
					double item1 = KnowledgeBase.attack_success(item2, item3);
					
					Tuple<double, int, int> t = new Tuple<double, int, int>(item1, item2, item3);

					action_description[worst_index] = t;
				}

				for(int i=0; i<success_by_units.Count; ++i)
				{
					double profit = success_by_units[i] * MapClass.bonuses[bonus].bonus_income * take_bonus_modifier;
					p.profit.Add(profit);
				}
				
				p.check_plan();
				plans.Add(p);
			}
			return plans;
		}
		
		private static List<Tuple<int, int>> attacking_plans(List<int> regions_in_bonus)	//from, to
		{
			List<Tuple<int, int>> attacks_locations = new List<Tuple<int, int>>();	//from, to
			List<Tuple<int, int>> free_armies = MapClass.existing_armies();		//all mine existing armies, first region id, second are armies over 1
			
			//for(int region_index = 0; region_index < regions_in_bonus.Count; ++region_index)
			foreach(int region_to_attack in regions_in_bonus)
			{
				if(MapClass.regions[region_to_attack].owner != OwnerEnum.Me)	//region in bonus which is not mine must be captured
				{
					List<int> my_neighbors = MapClass.my_neighbors(region_to_attack);
					
					if(my_neighbors.Count == 0)
						Utils.error_output("Error 11");

					bool action_added = false;
					
					for(int i=0; i<free_armies.Count; ++i)
					{
						if(my_neighbors.Contains(free_armies[i].Item1))	//use some existing armies if possible
						{
							attacks_locations.Add(new Tuple<int, int>(free_armies[i].Item1, region_to_attack));
							free_armies.RemoveAt(i);
							i--;
							action_added = true;
							break;
						}
					}

					if(!action_added)		//choose randomly
						attacks_locations.Add(new Tuple<int, int>(my_neighbors[Utils.rnd.Next(0, my_neighbors.Count)], region_to_attack));
				}
			}
			return attacks_locations;
		}
				
		private static List<Plan> defend_bonus()
		{
			List<Plan> plans = new List<Plan>();
			for(int bonus=1; bonus<MapClass.bonuses.Count; ++bonus)
			{
				bool my_bonus = true;
				foreach(int region_id in MapClass.bonuses[bonus].regions)
				{	
					if(MapClass.regions[region_id].owner != OwnerEnum.Me)
					{
						my_bonus = false;
						break;
					}
				}

				if(!my_bonus)
					continue;
					
				List<Tuple<int, int>> regions_in_danger = new List<Tuple<int, int>>();	//region_id and enemy armies in adjacent territories

				foreach(int region_id in MapClass.bonuses[bonus].regions)
				{
					List<int> dangerous_regions = MapClass.enemy_neighbors(region_id);
					int enemy_units = 0;
					if(dangerous_regions.Count > 0)
					{
						foreach(int enemy_region in dangerous_regions)
						{
							enemy_units += MapClass.regions[enemy_region].number_of_armies - 1;
						}
						regions_in_danger.Add(new Tuple<int, int>(region_id, enemy_units));
					}
				}

				if(regions_in_danger.Count == 0)
					continue;

				Plan p = new Plan();
				p.description = "defend bonus " + bonus;
				p.bonus_id = bonus;
				
				List<Tuple<double, int, int>> success_of_action = new List<Tuple<double, int, int>>();	//chance, my_armies, enemy_armies
				List<double> enemy_success = new List<double>();	//chance that enemy conquer my region if I will not defend it
				
				for(int i=0; i<regions_in_danger.Count; ++i)		//create actions
				{
					Action a = new Action();
					a.defend = true;
					a.existing_armies = MapClass.regions[regions_in_danger[i].Item1].number_of_armies - 1;
					a.from = regions_in_danger[i].Item1;
					p.actions.Add(a);
					if(a.existing_armies > 0)
						p.needed_existing_armies.Add(new Tuple<int, int>(a.from, a.existing_armies));
					
					int enemy_armies = regions_in_danger[i].Item2;
					int my_armies = a.existing_armies + 1;

					double enemy_attack = KnowledgeBase.enemy_successfull_attack(enemy_armies, my_armies);
					double my_defense = 1.0 - KnowledgeBase.enemy_successfull_attack(enemy_armies, my_armies);
					
					success_of_action.Add(new Tuple<double, int, int>(my_defense, my_armies, enemy_armies));
					enemy_success.Add(enemy_attack);
				}
				
				List<double> success_by_units = new List<double>();

				double enemy_takes_some_regions;	//given I will not defend my bonus
				double enemy_takes_nothing = 1.0;
				foreach(double enemy_takes_region in enemy_success)
				{
					enemy_takes_nothing = enemy_takes_nothing * (1.0 - enemy_takes_region);
				}
				enemy_takes_some_regions = 1.0 - enemy_takes_nothing;

				for(int i=0; i <= Bot.armies_to_deploy; ++i)
				{
					double success = 1.0;
					int worst_index = 0;
					double worst_chance = 1;

					for(int j=0; j<success_of_action.Count; ++j)
					{
						success = success * success_of_action[j].Item1;

						if(success_of_action[j].Item1 < worst_chance)
						{
							worst_chance = success_of_action[j].Item1;
							worst_index = j;
						}
					}
					p.units.Add(worst_index);
					success_by_units.Add(success);
	
					Tuple<double, int, int> t = new Tuple<double, int, int>(1.0 - KnowledgeBase.enemy_successfull_attack(success_of_action[worst_index].Item3, success_of_action[worst_index].Item2 + 1), success_of_action[worst_index].Item2 + 1, success_of_action[worst_index].Item3);
					success_of_action[worst_index] = t;
				}

				foreach(double chance in success_by_units)
				{
					p.profit.Add(chance * MapClass.bonuses[bonus].bonus_income * defend_region_modifier * enemy_takes_some_regions);
				}
				p.check_plan();
				plans.Add(p);
			}
			return plans;
		}

		private static List<Plan> spoil_bonus()
		{
			List<Plan> plans = new List<Plan>();
			
			for(int bonus=1; bonus < MapClass.bonuses.Count; ++bonus)
			{
				bool ok_bonus = true;
				double enemies_bonus_probability = 1.0;
				
				List<int> accessible_regions = new List<int>();

				foreach(int region_id in MapClass.bonuses[bonus].regions)
				{
					if(MapClass.regions[region_id].owner == OwnerEnum.Unknown)
					{
						enemies_bonus_probability = enemies_bonus_probability * KnowledgeBase.region_owner(region_id, OwnerEnum.Enemy);
					}
					else if(MapClass.regions[region_id].owner != OwnerEnum.Enemy)
					{
						ok_bonus = false;
						break;
					}
					else if(MapClass.regions[region_id].owner == OwnerEnum.Enemy)
						accessible_regions.Add(region_id);
				}
				
				if((!ok_bonus) || (accessible_regions.Count == 0))
					continue;

				int attacking_region = 0;
				int number_of_armies = 0;
				
				foreach(int region in accessible_regions)
				{
					foreach(int my_region in MapClass.my_neighbors(region))
					{
						if(MapClass.regions[region].number_of_armies > number_of_armies)
						{
							number_of_armies = MapClass.regions[my_region].number_of_armies;
							attacking_region = my_region;
						}
					}
				}
				
				List<int> regions_to_attack = MapClass.enemy_neighbors(attacking_region);

				Plan p = new Plan();
				p.description = "spoil bonus " + bonus;
				p.bonus_id = bonus;
				
				Action a = new Action();

				a.defend = false;
				a.from = attacking_region;
				a.to = regions_to_attack[Utils.rnd.Next(0, regions_to_attack.Count)];
				a.existing_armies = number_of_armies - 1;

				p.actions.Add(a);
				for(int i=0; i <= Bot.armies_to_deploy; ++i)
					p.units.Add(0);

				if(number_of_armies > 1)
					p.needed_existing_armies.Add(new Tuple<int, int>(a.from, number_of_armies - 1));
				
				for(int i=0; i <= Bot.armies_to_deploy; ++i)
				{
					double success = KnowledgeBase.attack_success(a.existing_armies + i, MapClass.regions[a.to].number_of_armies);
					success = success * enemies_bonus_probability;
					p.profit.Add(success * MapClass.bonuses[bonus].bonus_income * spoil_bonus_modifier);
				}
				p.check_plan();
				plans.Add(p);
			}
			return plans;
		}
		
		private static List<Plan> continue_spoiling()
		{
			List<Plan> plans = new List<Plan>();
			
			for(int bonus=1; bonus<MapClass.bonuses.Count; ++bonus)
			{
				bool bonus_ok = true;
				double ok_bonus_probability = 1.0;
				double enemy_successfull_attack_probability;	//probability that enemy will successfully conquer whole bonus if I will not do anything, will be estimated later

				List<int> enemy_regions = new List<int>();
				List<int> my_regions = new List<int>();

				foreach(int region in MapClass.bonuses[bonus].regions)
				{
					if(MapClass.regions[region].owner == OwnerEnum.Neutral)
					{
						bonus_ok = false;
						break;
					}
					else if(MapClass.regions[region].owner == OwnerEnum.Enemy)
						enemy_regions.Add(region);
					
					else if(MapClass.regions[region].owner == OwnerEnum.Me)
						my_regions.Add(region);

					else 
					{
						enemy_regions.Add(region);
						ok_bonus_probability = ok_bonus_probability * KnowledgeBase.region_owner(region, OwnerEnum.Enemy);
					}
				}

				if((enemy_regions.Count == 0) || (my_regions.Count == 0))
					bonus_ok = false;

				foreach(int region in my_regions)		
				{
					if(MapClass.enemy_neighbors(region).Count == 0)
					{
						bonus_ok = false;		//enemy cant take some region in bonus
						break;
					}
				}

				if(!bonus_ok)
					continue;
				
				List<int> already_counted_regions = new List<int>();
				int enemy_armies_nearby = 0;
				int my_armies = 0;
				foreach(int region in my_regions)
				{
					foreach(int adjacent_enemy in MapClass.enemy_neighbors(region))
					{
						if(!already_counted_regions.Contains(adjacent_enemy))
						{
							enemy_armies_nearby += MapClass.regions[adjacent_enemy].number_of_armies - 1;
							already_counted_regions.Add(adjacent_enemy);
						}
					}
					my_armies += MapClass.regions[region].number_of_armies;
				}
				
				enemy_armies_nearby += KnowledgeBase.enemy_income();		//this is probably lower but I dont know any good estimation
				enemy_successfull_attack_probability = KnowledgeBase.attack_success(enemy_armies_nearby, my_armies);

				int region_to_recruit = my_regions[Utils.rnd.Next(0, my_regions.Count)];
			
				int weakest_army = 1000000;
				int weakest_enemy_region = 0;
				int enemies_nearby = 0;

				foreach(int adjacent_enemy in MapClass.enemy_neighbors(region_to_recruit))
				{
					enemies_nearby += MapClass.regions[adjacent_enemy].number_of_armies - 1;
					if(MapClass.regions[adjacent_enemy].number_of_armies < weakest_army)
					{
						weakest_army = MapClass.regions[adjacent_enemy].number_of_armies;
						weakest_enemy_region = adjacent_enemy;
					}
				}

				Plan p = new Plan();
				p.description = "continue spoiling bonus " + bonus;
				p.bonus_id = bonus;
				Action a = new Action();
				
				a.defend = false;
				a.from = region_to_recruit;
				a.to = weakest_enemy_region;
				a.existing_armies = MapClass.regions[region_to_recruit].number_of_armies - 1;
				if(a.existing_armies > 0)
					p.needed_existing_armies.Add(new Tuple<int, int>(region_to_recruit, a.existing_armies));

				p.actions.Add(a);
				
				for(int i=0; i <= Bot.armies_to_deploy; ++i)
				{
					double success = 1.0 - KnowledgeBase.enemy_successfull_attack(enemies_nearby, MapClass.regions[region_to_recruit].number_of_armies + i);
					if(success > 1.0)
						Utils.error_output("Error 21");

					success = success * MapClass.bonuses[bonus].bonus_income * continue_spoiling_modifier * enemy_successfull_attack_probability;
					p.profit.Add(success);
					p.units.Add(0);
				}
				
				p.check_plan();
				plans.Add(p);
			}
			return plans;
		}
			
		private static List<Plan> take_region()
		{
			List<Plan> plans = new List<Plan>();
			for(int bonus=1; bonus<MapClass.bonuses.Count; ++bonus)
			{
				for(int number_of_actions = 1; number_of_actions <= MapClass.bonuses[bonus].regions.Count; ++number_of_actions)
				{
					bool bonus_ok = false;

					List<int> enemy_regions_to_attack = new List<int>();
					List<int> neutral_regions_to_attack = new List<int>();
					List<int> unknown_regions_in_bonus = new List<int>();

					foreach(int region in MapClass.bonuses[bonus].regions)
					{
						if(MapClass.regions[region].owner == OwnerEnum.Enemy)
						{
							bonus_ok = true;
							enemy_regions_to_attack.Add(region);
						}
						else if(MapClass.regions[region].owner == OwnerEnum.Neutral)
						{
							bonus_ok = true;
							neutral_regions_to_attack.Add(region);
						}
						else if(MapClass.regions[region].owner == OwnerEnum.Unknown)
						{
							unknown_regions_in_bonus.Add(region);
						}
					}

					if((!bonus_ok) || (enemy_regions_to_attack.Count + neutral_regions_to_attack.Count < number_of_actions))
						continue;

					enemy_regions_to_attack.Sort(delegate(int r1, int r2) 
							{return MapClass.regions[r1].number_of_armies.CompareTo(MapClass.regions[r2].number_of_armies);});
					
					neutral_regions_to_attack.Sort(delegate(int r1, int r2) 
							{return MapClass.regions[r1].number_of_armies.CompareTo(MapClass.regions[r2].number_of_armies);});

					if((enemy_regions_to_attack.Count > 2) && 
						(MapClass.regions[enemy_regions_to_attack[1]].number_of_armies < MapClass.regions[enemy_regions_to_attack[0]].number_of_armies))
					{
						Utils.error_output("Error 14");		//badly sorted array, should be ascending
					}

					
					enemy_regions_to_attack.AddRange(neutral_regions_to_attack);
					List<Tuple<int, int>> attack_descriptions = attacking_plans(enemy_regions_to_attack);

					int remaining_armies = 0;
					for(int i=number_of_actions; i<enemy_regions_to_attack.Count; ++i)
						remaining_armies += MapClass.regions[enemy_regions_to_attack[i]].number_of_armies;

					foreach(int region_id in unknown_regions_in_bonus)
					{
						if(MapClass.regions[region_id].wasteland)
							remaining_armies += 10;
						else
							remaining_armies += 2;
					}

					Plan p = new Plan();
					p.description = "take region in bonus " + bonus;
					p.bonus_id = bonus;

					List<Tuple<int, int>> free_armies = MapClass.existing_armies();
					List<Tuple<double, int, int>> success_by_action = new List<Tuple<double, int, int>>();
					
					for(int action_index = 0; action_index < number_of_actions; ++action_index)
					{
						Action a = new Action();
						a.from = attack_descriptions[action_index].Item1;
						a.to = attack_descriptions[action_index].Item2;
						a.defend = false;
						a.existing_armies = 0;
						for(int i=0; i<free_armies.Count; ++i)
						{
							if(a.from == free_armies[i].Item1)
							{
								a.existing_armies = free_armies[i].Item2;
								p.needed_existing_armies.Add(free_armies[i]);
								free_armies.RemoveAt(i);
								break;
							}
						}
						p.actions.Add(a);
						int defending = MapClass.regions[a.to].number_of_armies;
						success_by_action.Add(new Tuple<double, int, int>(KnowledgeBase.attack_success(a.existing_armies, defending), a.existing_armies, defending));
					}
					
					List<double> success_by_units = new List<double>();

					for(int i=0; i <= Bot.armies_to_deploy; ++i)
					{
						double success = 1.0;
						double worst_success = 1.0;
						int worst_index = 0;
						for(int j=0; j<success_by_action.Count; ++j)
						{
							success = success * success_by_action[j].Item1;
							if(worst_success > success_by_action[j].Item1)
							{
								worst_success = success_by_action[j].Item1;
								worst_index = j;
							}
						}

						success_by_units.Add(success);
						p.units.Add(worst_index);
						
						int attackers = success_by_action[worst_index].Item2 + 1;
						int defenders = success_by_action[worst_index].Item3;
						success_by_action[worst_index] = new Tuple<double, int, int>(KnowledgeBase.attack_success(attackers, defenders), attackers, defenders);
					}

					for(int i=0; i<success_by_units.Count; ++i)
					{
						double profit;
						if(remaining_armies > 0)
							profit = success_by_units[i] * take_region_modifier * MapClass.bonuses[bonus].bonus_income 
										/ (remaining_armies * remaining_armies);
						else
							profit = success_by_units[i] * take_region_modifier * MapClass.bonuses[bonus].bonus_income;

						p.profit.Add(profit);
					}
					p.check_plan();
					plans.Add(p);
				}
			}
			return plans;
		}
		
		//private List<Plan> fight_enemy();		//todo
		//private List<Plan> explore_to_spoil();	//todo
		
		private static List<Tuple<Plan, int>> choose_plans(List<Plan> plans)
		{
			List<Tuple<int, int>> free_armies = MapClass.existing_armies();
			List<Tuple<int, int>> chosen_plans = new List<Tuple<int, int>>();	//plan index, armies_used
			List<Tuple<Plan, int>> result = new List<Tuple<Plan, int>>();

			int armies_to_use = Bot.armies_to_deploy;
			while(armies_to_use > 0)
			{
				Tuple<int, int> best_usable_plan = best_plan(chosen_plans, free_armies, plans, armies_to_use);

				if(best_usable_plan.Item1 == -1)
				{
					Utils.error_output("Not enough plans...");
					if(chosen_plans.Count > 0)		//first plan will be used with all remaining units
					{
						chosen_plans[0] = new Tuple<int, int>(chosen_plans[0].Item1, chosen_plans[0].Item2 + armies_to_use);
						armies_to_use = 0;
					}
					else
					{
						plans.Add(Plan.random_plan());
						chosen_plans.Add(new Tuple<int, int>(plans.Count - 1, armies_to_use));
						armies_to_use = 0;
					}
					break;
				}
				
				int free_armies_replacement = 0;
				Plan p = plans[best_usable_plan.Item1];
				foreach(Tuple<int, int> t in p.needed_existing_armies)
				{
					bool founded = false;
					for(int i=0; i<free_armies.Count; ++i)
					{
						if(t.Item1 == free_armies[i].Item1)
						{
							founded = true;
							Tuple<int, int> remaining = new Tuple<int, int>(t.Item1, free_armies[i].Item2 - t.Item2);
							if(remaining.Item2 <= 0)
							{
								free_armies_replacement += t.Item2 - free_armies[i].Item2;
								free_armies.RemoveAt(i);
							}
							break;
						}
					}
					if(!founded)
						free_armies_replacement += t.Item2;		
				}
				
				chosen_plans.Add(best_usable_plan);
				armies_to_use -= free_armies_replacement + best_usable_plan.Item2;

				if(armies_to_use < 0)
					Utils.error_output("Error 15");
			}
	
			for(int i=0; i<chosen_plans.Count; ++i)
				result.Add(new Tuple<Plan, int>(plans[chosen_plans[i].Item1], chosen_plans[i].Item2));
			
			return result;
		}

		private static Tuple<int, int> best_plan(List<Tuple<int, int>> chosen_plans, List<Tuple<int, int>> free_armies, List<Plan> all_plans, int armies_to_use)
		{
			List<int> used_bonuses = new List<int>();
			foreach(Tuple<int, int> t in chosen_plans)
				used_bonuses.Add(all_plans[t.Item1].bonus_id);

			double best_profit_per_unit = -10000;
			int best_plan = -1;
			int best_number_of_units = -1;
			for(int i=0; i<all_plans.Count; ++i)
			{
				if(used_bonuses.Contains(all_plans[i].bonus_id))
				{
					continue;
				}

				int extra_units = 0;
				foreach(Tuple<int, int> t in all_plans[i].needed_existing_armies)
				{
					bool founded = false;
					for(int j=0; j<free_armies.Count; ++j)
					{
						if(free_armies[j].Item1 == t.Item1)
						{
							if(free_armies[j].Item2 < t.Item2)	//needs more free armies than are available
								extra_units += t.Item2 - free_armies[j].Item2;

							founded = true;
							break;
						}
					}
					if(!founded)
						extra_units += t.Item2;
				}

				for(int units = 1; units < armies_to_use - extra_units; ++units)	
				{	//following formula doesnt work well with 0 units, thus I decided to ignore 0 units plans
					double p = all_plans[i].profit[units];
					double u = units + extra_units;
					double profit_per_unit = p / u;

					if(profit_per_unit > best_profit_per_unit)
					{
						best_profit_per_unit = profit_per_unit;
						best_plan = i;
						best_number_of_units = units;
					}
				}
			}
			return new Tuple<int, int>(best_plan, best_number_of_units);
		}


		public static void apply_plans(List<Tuple<Plan, int>> plans_to_apply)
		{
			List<Tuple<int, int, int>> actions = new List<Tuple<int, int, int>>();	//from, to, number of armies, if from == to, than dont move
			
			for(int plan=0; plan<plans_to_apply.Count; ++plan)		//generate actions
			{
				Utils.error_output("PLAN: " + plans_to_apply[plan].Item1.description);
				for(int action=0; action<plans_to_apply[plan].Item1.actions.Count; ++action)
				{
					Action a = plans_to_apply[plan].Item1.actions[action];
					int from = a.from;
					int to;
					if(a.defend)
						to = from;
					else
						to = a.to;
					int armies = a.existing_armies;

					for(int i=0; i<plans_to_apply[plan].Item2; ++i)
					{
						if(action == plans_to_apply[plan].Item1.units[i])
						{
							armies++;
						}
					}
					actions.Add(new Tuple<int, int, int>(from, to, armies));
					if(armies == 0)
					{
						Utils.error_output("Error 21");
					}
				}
			}
			
			List<Tuple<int, int>> free_armies = MapClass.existing_armies();
			List<int> deployment_list = new List<int>();
			
			for(int i=0; i<MapClass.regions.Count; ++i)
				deployment_list.Add(0);	

			for(int i=0; i<actions.Count; ++i)			//find right deployments
			{
				bool founded = false;
				for(int j=0; j<free_armies.Count; ++j)
				{
					if(actions[i].Item1 == free_armies[j].Item1)
					{
						founded = true;
						int extra_free_armies = free_armies[j].Item2 - actions[i].Item3;
						if(extra_free_armies <= 0)
						{
							int armies_to_deploy = - extra_free_armies;
							deployment_list[actions[i].Item1] += armies_to_deploy;
							free_armies.RemoveAt(j);
						}
						else
						{
							free_armies[j] = new Tuple<int, int>(free_armies[j].Item1, free_armies[j].Item2 - actions[i].Item3);
						}
					}
				}
				if(!founded)
				{
					deployment_list[actions[i].Item1] += actions[i].Item3;
				}
			}
			
			List<Tuple<int, int, int>> other_actions = unused_armies_action(free_armies);
			foreach(Tuple<int, int, int> in other_actions)
			{
				if(t.Item3 == 0)
					Utils.error_output("Error 22");
			}
			actions.AddRange(other_actions);

			/*
			foreach(Tuple<int, int> t in free_armies)		//move unused existing armies randomly
			{
				List<int> my_list = MapClass.my_neighbors(t.Item1);
				List<int> enemy_list = MapClass.enemy_neighbors(t.Item1);
				if((my_list.Count > 0) && (enemy_list.Count == 0))
				{
					int how_many = t.Item2;
					int to = my_list[Utils.rnd.Next(0, my_list.Count)];
					int from = t.Item1;
					actions.Add(new Tuple<int, int, int>(from, to, how_many));
					Utils.error_output("free unused armies in " + from);
				}
			}
			*/

			planned_deployments.Clear();
			planned_actions.Clear();

			foreach(Tuple<int, int, int> t in actions)
			{
				if(t.Item1 != t.Item2)
				{
					planned_actions.Add(t);
				}
			}

			for(int i=0; i<deployment_list.Count; ++i)
			{
				if(deployment_list[i] != 0)
				{
					planned_deployments.Add(new Tuple<int, int>(i, deployment_list[i]));
				}
			}
		}

		public static List<Tuple<int, int, int>> unused_armies_action(List<Tuple<int, int>> free_armies)
		{
			List<Tuple<int, int, int>> actions = new List<Tuple<int, int, int>>();
			foreach(Tuple<int, int> t in free_armies)
			{
				if(MapClass.enemy_neighbors(t.Item1).Count > 0)
					continue;
				
				List<Tuple<int, int>> frontier = new List<Tuple<int, int>>();		//current region, first region of path
				List<bool> explored = new List<bool>();
				for(int i=0; i<MapClass.regions.Count; ++i)
				{
					explored.Add(false);
				}
				explored[t.Item1] = true;

				foreach(int my_neighbor in MapClass.my_neighbors(t.Item1))
				{
					frontier.Add(new Tuple<int, int>(my_neighbor, my_neighbor));
					explored[my_neighbor] = true;
				}
				
				int index = 0;
				while(frontier.Count > index)
				{
					if(MapClass.enemy_neighbors(frontier[index].Item1).Count > 0)	//found path to enemy
					{
						actions.Add(new Tuple<int, int, int>(t.Item1, frontier[index].Item2, t.Item2));
						break;
					}
					else
					{
						List<int> neighbors = MapClass.my_neighbors(frontier[index].Item1);
						foreach(int region in neighbors)
						{
							if(!explored[region])
							{
								frontier.Add(new Tuple<int, int>(region, frontier[index].Item2));
								explored[region] = true;
							}
						}
						index++;
					}
				}
			}
			return actions;
		}

		public static void print_plans_info(List<Plan> plans)
		{
			foreach(Plan p in plans)
			{
				StringBuilder s = new StringBuilder();
				s.Append(p.description);
				s.Append(" - profit: ");
				for(int i=1; i<p.profit.Count; ++i)
				{
					s.Append(p.profit[i] + " ");
				}
				Utils.error_output(s.ToString());
			}
		}

		public static void decide_actions()
		{
			List<Plan> all_plans = new List<Plan>();
			all_plans.AddRange(take_bonus());
			all_plans.AddRange(defend_bonus());
			all_plans.AddRange(spoil_bonus());
			all_plans.AddRange(continue_spoiling());
			all_plans.AddRange(take_region());

			print_plans_info(all_plans);

			List<Tuple<Plan, int>> plans_to_apply = choose_plans(all_plans);

			apply_plans(plans_to_apply);
		}

		static double take_bonus_modifier = 1.0;
		static double defend_region_modifier = 1.0;
		static double spoil_bonus_modifier = 1.0;
		static double continue_spoiling_modifier = 1.0;
		static double take_region_modifier = 0.5;

		public static List<Tuple<int, int>> planned_deployments = new List<Tuple<int, int>>();
		public static List<Tuple<int, int, int>> planned_actions = new List<Tuple<int, int, int>>();
	}

	struct Action
	{
		public bool defend;		//if true, than "to" is -1
		public int from;
		public int to;
		public int existing_armies;
	}

	class Plan
	{
		public int bonus_id;
		public List<Action> actions = new List<Action>();
		public List<int> units = new List<int>();				//which action should have n-th unit
		public List<Tuple<int, int>> needed_existing_armies = new List<Tuple<int, int>>();		//where and how many
		public List<double> profit = new List<double>();
		public string description;

		public void check_plan()
		{
			foreach(Action a in actions)
			{
				if((((a.to == 0) || (a.to >= MapClass.regions.Count)) && (a.defend == false)) ||
					((a.from == 0) || (a.from >= MapClass.regions.Count)))
				{
					Utils.error_output("Error 16");
				}
				else if(MapClass.regions[a.from].owner != OwnerEnum.Me)
				{
					Utils.error_output("Error 20");
				}
			}
			
			foreach(int i in units)
			{
				if((i < 0) || (i >= actions.Count))
					Utils.error_output("Error 17");
			}
			foreach(Tuple<int, int> t in needed_existing_armies)
			{
				if((t.Item1 == 0) || (t.Item1 >= MapClass.regions.Count))
				{
					Utils.error_output("Error 19");
				}
			}
		}

		public static Plan random_plan()
		{
			Plan p = new Plan();
			p.description = "random plan";
			for(int i=1; i<MapClass.regions.Count; ++i)
			{
				if(MapClass.regions[i].owner == OwnerEnum.Me)
				{
					Action a = new Action();
					a.from = i;
					a.defend = true;
					a.existing_armies = 0;
					p.actions.Add(a);
					
					for(int j=1; j<MapClass.bonuses.Count; ++j)
					{
						if(MapClass.bonuses[j].regions.Contains(i))
						{
							p.bonus_id = j;
							break;
						}
					}
					break;
				}
			}
			for(int i=0; i <= Bot.armies_to_deploy; ++i)
			{
				p.units.Add(0);
				p.profit.Add(0);
			}
			return p;
		}
	}

	class KnowledgeBase
	{
		static public double region_owner(int region, OwnerEnum owner)
		{
			if(enemy_regions.Count == 0)
				enemy_regions = create_enemy_territories();

			if(MapClass.regions[region].owner == owner)
				return 1.0;
			
			else if(MapClass.regions[region].owner == OwnerEnum.Unknown)
			 	return enemy_regions[region];
			
			else if(MapClass.regions[region].owner != owner)
				return 0.0;
			
			return 0.0;
		}
				
		static public double enemy_bonus(int bonus)
		{
			double probability = 1.0;
			foreach(int region in MapClass.bonuses[bonus].regions)
			{
				probability = probability * region_owner(region, OwnerEnum.Enemy);
			}
			return probability;
		}
		
		static public double enemy_recruit(int number_of_armies)	//enemy will recruit at least number_of_armies on some important location...
		{
			if(number_of_armies > enemy_income())
				return 0;

			double number_of_enemy_regions = 0;
			for(int region = 1; region < MapClass.regions.Count; ++region)
				number_of_enemy_regions += region_owner(region, OwnerEnum.Enemy);	

			double number_of_recruitments = (double)enemy_income() / (double)(number_of_armies);

			double result = number_of_recruitments / number_of_enemy_regions;

			if(result > 1)
				return 1.0;

			return result;
		}

		static public int enemy_income()
		{
			return enemy_income_number;
		}

		static public double attack_success(int attacking, int defending)
		{
			if((attacking < attack_success_table_size) && (defending < attack_success_table_size))
				return attack_success_table[attacking][defending];
			
			else
			{
				if((0.6*attacking) > defending)
					return 1.0;
				else
					return 0.0;
			}
		}

		static public double enemy_successfull_attack(int enemy_attacking, int me_defending)		//including possible enemy's recruitment
		{
			double p = 0;
			for(int i=0; i<enemy_income(); ++i)
			{
				p += (enemy_recruit(i) - enemy_recruit(i + 1)) * attack_success(enemy_attacking + i, me_defending);
			}
			if((p > 1) || (p < 0))
				Utils.error_output("error 19");

			return p;
		}

		static private double combination_number(int whole_size, int subset_size)
		{
			if(subset_size < whole_size/2)
			{
				subset_size = whole_size - subset_size;
			}
			
			double result = 1;
			int j = 0;
			for(int i=whole_size; i > subset_size; --i)		//this computes whole_size! / (subset_size! * (whole_size - subset_size)!	
			{							//but hopefully with small rounding errors
				result = result * i;
				result = result / (whole_size - subset_size - j);
				++j;
			}
			return result;
		}

		static private List<double> armies_destroyed(int armies, bool attack)	//returns probabilities that "armies" will destroy more than "index" enemies
		{
			List<double> luck = new List<double>();
			List<Tuple<double, double>> chance_to_destroy = new List<Tuple<double, double>>();	//first number of armies, second chance
			List<double> result = new List<double>();

			double p;
			if(attack)
				p = 0.6;
			else
				p = 0.7;

			for(int i=0; i<armies; ++i)
			{
				//double chance = Math.Pow(p, i) * combination_number(armies, i) * Math.Pow(1.0 - p, armies - i);
				double p1 = Math.Pow(p, i);
				double p2 = combination_number(armies, i);
				double p3 = Math.Pow(1.0 - p, armies - i);
				double chance = p1 * p2 * p3;
				luck.Add(chance);
			}
				
			double no_luck = p * armies;

			for(int i=0; i<armies; ++i)
				chance_to_destroy.Add(new Tuple<double, double>((double)(i)*0.16 + no_luck*0.84, luck[i]));
			
			double probability = 1.0;
			int index = 0;
			int rounded_armies = 0;
			while(index < chance_to_destroy.Count)
			{
				int rounded = (int)Math.Round(chance_to_destroy[index].Item1);
				if(rounded_armies <= rounded)
				{
					result.Add(probability);
					if(probability != result[result.Count - 1])
					{
						Utils.error_output("Mega error");
					}
					rounded_armies++;
				}
				else if(rounded_armies > rounded)
				{
					probability -= chance_to_destroy[index].Item2;
					++index;
				}
			}
			return result;
		}

		private static List<List<double>> load_attack_success_table()
		{
			List<List<double>> result = new List<List<double>>();
			for(int i=0; i<attack_success_table_size; ++i)
			{
				List<double> chances_with_n_armies = armies_destroyed(i, true);

				for(int j=chances_with_n_armies.Count; j<attack_success_table_size; ++j)
					chances_with_n_armies.Add(0);

				result.Add(chances_with_n_armies);
			}
			return result;
		}
		
		public static void enemy_starting_regions(int region_id)
		{
			if(enemy_regions.Count == 0)	//not initialized
			{
				enemy_regions = create_enemy_territories();
			}
			enemy_regions[region_id] = 1.0;
		}
	
		public static void possible_enemy_starting_regions(int region_id)
		{	
			if(enemy_regions.Count == 0)	//not initialized
			{
				enemy_regions = create_enemy_territories();
			}
		
			enemy_regions[region_id] = 0.5;
		}

		public static void update()
		{
			List<double> new_probabilities = new List<double>();
			new_probabilities.Add(0);		//0-th nonexistent region

			for(int region=1; region < MapClass.regions.Count; ++region)
			{
				if(MapClass.regions[region].owner == OwnerEnum.Unknown)
				{
					List<double> adjacent_enemy = new List<double>();
					
					foreach(int neighbor in MapClass.neighbors(region))
					{
						if(region_owner(neighbor, OwnerEnum.Enemy) > 0)
						{
							double attack_chance = region_owner(neighbor, OwnerEnum.Enemy);
							if(MapClass.regions[region].wasteland)
								attack_chance = attack_chance * enemy_recruit(14);	//how much armies must attack wasteland
							else 
								attack_chance = attack_chance * enemy_recruit(3);	//how much armies must attack normal territory

							adjacent_enemy.Add(attack_chance);
						}
					}
					
					double p = 1.0 - region_owner(region, OwnerEnum.Enemy);

					foreach(double d in adjacent_enemy)
					{
						p = p * (1.0 - d);			//p is probability that region is not owned by enemy
					}
					new_probabilities.Add(1.0 - p);
				}
				else if(MapClass.regions[region].owner == OwnerEnum.Enemy)
					new_probabilities.Add(1.0);
				
				else
					new_probabilities.Add(0);
			}

			enemy_regions = new_probabilities;

			enemy_income_number = compute_enemy_income();
		}

		private static int compute_enemy_income()
		{
			double income = 5;
			for(int i=1; i<MapClass.bonuses.Count; ++i)
			{
				income += enemy_bonus(i) * MapClass.bonuses[i].bonus_income;
			}
			
			return (int)Math.Round(income);
		}

		private static List<double> create_enemy_territories()
		{
			List<double> l = new List<double>();
			for(int i=0; i<MapClass.regions.Count; ++i)
			{
				l.Add(0.0);
			}
			return l;
		}
		

		const int attack_success_table_size = 20;	//attack_success_table[attacker_armies][defender_armies] = probability of taking territory
		private static List<List<double>> attack_success_table = load_attack_success_table();
		private static List<double> enemy_regions = new List<double>();
		private static int enemy_income_number = 5;
	}
}







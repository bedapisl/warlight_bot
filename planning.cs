using System;
using System.Collections.Generic;
using System.Text;

namespace warlight
{
	class Planning
	{
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

			double best_profit_per_unit = 0;
			if(chosen_plans.Count == 0)
				best_profit_per_unit = -100000;
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

				for(int units = 1; units <= armies_to_use - extra_units; ++units)	
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
					foreach(Action supporting in a.supporting_actions)
						actions.Add(new Tuple<int, int, int>(supporting.from, supporting.to, supporting.existing_armies));
					
					int from = a.from;
					int to;
					if(a.defend)
						to = from;
					else
						to = a.to;
					int armies = a.existing_armies;

					for(int i=0; i<plans_to_apply[plan].Item2; ++i)
					{
						if(action == plans_to_apply[plan].Item1.units_allocation[i])
							armies++;
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
			foreach(Tuple<int, int, int> t in other_actions)
			{
				if(t.Item3 == 0)
					Utils.error_output("Error 22");
			}
			actions.AddRange(other_actions);

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
				int first_region_of_path = MapClass.path_to_enemy(t.Item1);
				if(first_region_of_path == -1)
					first_region_of_path = MapClass.path_to_neutral(t.Item1);
				
				if((first_region_of_path != -1) && (first_region_of_path != t.Item1))
					actions.Add(new Tuple<int, int, int>(t.Item1, first_region_of_path, t.Item2));

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
			all_plans.AddRange(take_bonus.generate_plans());
			all_plans.AddRange(defend_bonus.generate_plans());
			all_plans.AddRange(spoil_bonus.generate_plans());
			all_plans.AddRange(continue_spoiling.generate_plans());
			all_plans.AddRange(take_region.generate_plans());

			print_plans_info(all_plans);

			List<Tuple<Plan, int>> plans_to_apply = choose_plans(all_plans);

			apply_plans(plans_to_apply);
		}

		static public TakeBonusPlanGenerator take_bonus = new TakeBonusPlanGenerator();
		static public DefendBonusPlanGenerator defend_bonus = new DefendBonusPlanGenerator();
		static public SpoilBonusPlanGenerator spoil_bonus = new SpoilBonusPlanGenerator();
		static public ContinueSpoilingPlanGenerator continue_spoiling = new ContinueSpoilingPlanGenerator();
		static public TakeRegionPlanGenerator take_region = new TakeRegionPlanGenerator();

		static public double take_bonus_modifier = 1.0;
		static public double defend_bonus_modifier = 1.0;
		static public double aggressive_defense_modifier = 1.0;
		static public double spoil_bonus_modifier = 1.0;
		static public double continue_spoiling_modifier = 1.0;
		static public double take_region_modifier = 0.5;

		public static List<Tuple<int, int>> planned_deployments = new List<Tuple<int, int>>();
		public static List<Tuple<int, int, int>> planned_actions = new List<Tuple<int, int, int>>();
	}

	abstract class AbstractPlanGenerator
	{
		public abstract List<Plan> generate_plans();

		public virtual List<Tuple<int, int, int>> attacking_plans(List<int> regions_to_attack)	//from, to
		{
			List<Tuple<int, int>> attack_sources = new List<Tuple<int, int>>();
			List<Tuple<int, int>> attack_destinations = new List<Tuple<int, int>>();
			List<Tuple<int, int>> connections = new List<Tuple<int, int>>();

			foreach(int region in regions_to_attack)
			{
				if(MapClass.regions[region].owner == OwnerEnum.Me)
					Utils.error_output("Error 27");

				foreach(int my_neighbor in MapClass.my_neighbors(region))
				{
					connections.Add(new Tuple<int, int>(my_neighbor, region));
					Tuple<int, int> t = new Tuple<int, int>(my_neighbor, MapClass.regions[my_neighbor].number_of_armies - 1);
					if(!attack_sources.Contains(t))
					{
						attack_sources.Add(t);
					}
				}
				int armies_needed_to_attack = (int)Math.Round((1.0 / 0.6) * ((double) MapClass.regions[region].number_of_armies));
				attack_destinations.Add(new Tuple<int, int>(region, armies_needed_to_attack));
			}

			List<Tuple<int, int, int>> maximal_flow = Functional.maximal_flow(attack_sources, attack_destinations, connections);
			List<Tuple<int, int, int>> attacks = new List<Tuple<int, int, int>>();

			foreach(int region in regions_to_attack) //add edges with nonzero flow and edges with zero flow for regions dont have any nonzero flow edge
			{
				bool nonzero_flow_exist = false;
				Tuple<int, int, int> some_attack_to_region = new Tuple<int, int, int>(-1, -1, -1);

				foreach(Tuple<int, int, int> flow_edge in maximal_flow)
				{
					if(flow_edge.Item2 == region)
					{
						some_attack_to_region = flow_edge;
						if(flow_edge.Item3 > 0)
						{
							attacks.Add(flow_edge);
							nonzero_flow_exist = true;
						}
					}
				}

				if(!nonzero_flow_exist)
					attacks.Add(some_attack_to_region);
			}
			return attacks;
		}

		public Action generate_action(int bonus)		//action to attack one region in bonus
		{
			Action a = new Action();
			double best_ratio = 0;
			foreach(int enemy_region in MapClass.bonuses[bonus].regions)
			{
				if(MapClass.regions[enemy_region].owner == OwnerEnum.Enemy)
				{
					foreach(int my_region in MapClass.my_neighbors(enemy_region))
					{
						double ratio = ((double)MapClass.regions[my_region].number_of_armies) / ((double)MapClass.regions[enemy_region].number_of_armies);
						if(ratio > best_ratio)
						{
							best_ratio = ratio;
							a.from = my_region;
							a.to = enemy_region;
						}
					}
				}
			}
			a.defend = false;
			a.existing_armies = MapClass.regions[a.from].number_of_armies - 1;
			return a;
		}
		
		public virtual List<Action> generate_attacking_actions(List<Tuple<int, int, int>> attacks)	//attacks: from, to, existing_armies
		{
			foreach(Tuple<int, int, int> t in attacks)
			{
				if((t.Item1 <= 0) || (t.Item2 <= 0) || (t.Item3 < 0))	
					Utils.error_output("Error 28");
			}

			List<Action> actions = new List<Action>();
			
			while(attacks.Count > 0)
			{
				List<Tuple<int, int, int>> same_region_attacks = new List<Tuple<int, int, int>>();
				foreach(Tuple<int, int, int> t in attacks)
				{
					if(t.Item2 == attacks[0].Item2)
						same_region_attacks.Add(t);
				}

				foreach(Tuple<int, int, int> t in same_region_attacks)
				{
					attacks.Remove(t);
				}

				same_region_attacks.Sort(delegate(Tuple<int, int, int> t1, Tuple<int, int, int> t2) 
								{return t1.Item3.CompareTo(t2.Item3);});	//sort by existing armies, first should be biggest
				
				Action a = new Action();
				a.from = same_region_attacks[0].Item1;
				a.to = same_region_attacks[0].Item2;
				a.defend = false;
				a.existing_armies = same_region_attacks[0].Item3;

				for(int i=1; i<same_region_attacks.Count; ++i)
				{
					Action supporting = new Action();
					supporting.from = same_region_attacks[i].Item1;
					supporting.to = same_region_attacks[i].Item2;
					supporting.defend = false;
					supporting.existing_armies = same_region_attacks[i].Item3;
					a.supporting_actions.Add(supporting);
				}
				actions.Add(a);
			}
			return actions;
		}
			
		public virtual List<Tuple<int, int>> compute_needed_existing_units(List<Action> actions)
		{
			List<Tuple<int, int>> needed_existing_units = new List<Tuple<int, int>>();
			foreach(Action a in actions)
			{
				if(a.existing_armies > 0)
					needed_existing_units.Add(new Tuple<int, int>(a.from, a.existing_armies));
			}
			return needed_existing_units;
		}
		
		public virtual void compute_success_by_units(List<Action> actions, out List<double> success_by_units, out List<int> units_allocation)
		{
			List<int> extra_armies = new List<int>();
			success_by_units = new List<double>();
			units_allocation = new List<int>();

			foreach(Action a in actions)
				extra_armies.Add(0);
		
			for(int i=0; i <= Bot.armies_to_deploy; ++i)
			{
				int worst_index = 0;
				double worst_success = 1.0;
				double current_success = 1.0;

				for(int action_index = 0; action_index < actions.Count; ++action_index)
				{
					double action_success = KnowledgeBase.compute_action_success(actions[action_index], extra_armies[action_index]);
					current_success = current_success * action_success;

					if(action_success < worst_success)
					{
						worst_success = action_success;
						worst_index = action_index;
					}
				}

				success_by_units.Add(current_success);
				extra_armies[worst_index]++;
				units_allocation.Add(worst_index);
			}
		}

		public virtual Plan create_plan(List<Action> actions, int bonus_id, string description, double modifier)
		{
			Plan p = new Plan();	
				
			List<double> success_by_units;
			List<int> units_allocation;
			
			p.actions = actions;
			p.description = description;
			p.bonus_id = bonus_id;
			p.needed_existing_armies = compute_needed_existing_units(actions);
			compute_success_by_units(actions, out success_by_units, out units_allocation);
			p.units_allocation = units_allocation;

			for(int i=0; i<success_by_units.Count; ++i)
			{
				double profit = success_by_units[i] * MapClass.bonuses[bonus_id].bonus_income * modifier;
				p.profit.Add(profit);
			}
			
			p.check_plan();
			return p;
		}
	}

	class TakeBonusPlanGenerator : AbstractPlanGenerator
	{
		public override List<Plan> generate_plans()
		{
			List<Plan> plans = new List<Plan>();
			foreach(int bonus in prerequisities())
			{
				List<int> regions_to_attack = new List<int>();

				foreach(int region in MapClass.bonuses[bonus].regions)
				{
					if((MapClass.regions[region].owner == OwnerEnum.Enemy) || (MapClass.regions[region].owner == OwnerEnum.Neutral))
						regions_to_attack.Add(region);
				}

				List<Tuple<int, int, int>> attacks_locations = attacking_plans(regions_to_attack);	//attacks sources and destinations
				//List<Action> actions = use_free_armies_to_attack(attacks_locations);
				List<Action> actions = generate_attacking_actions(attacks_locations);

				Plan p = create_plan(actions, bonus, "take bonus " + bonus, Planning.take_bonus_modifier);
				
				plans.Add(p);
			}
			return plans;
		}

		public virtual List<int> prerequisities()
		{	
			List<int> ok_bonuses = new List<int>();
			for(int bonus=1; bonus < MapClass.bonuses.Count; ++bonus)
			{
				bool ok_plan = true;
				bool my_bonus = true;
					
				foreach(int region_in_bonus in MapClass.bonuses[bonus].regions)			//check if every region in bonus is reachable
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
				
				if((ok_plan) && (!my_bonus))
					ok_bonuses.Add(bonus);
			}
			return ok_bonuses;
		}		
	}

	class DefendBonusPlanGenerator : AbstractPlanGenerator
	{
		public override List<Plan> generate_plans()
		{
			List<Plan> plans = new List<Plan>();
			foreach(int bonus in prerequisities())
			{
				List<int> regions_in_danger = compute_regions_in_danger(bonus);
				List<int> dangerous_regions = compute_dangerous_regions(bonus);
				List<Action> aggressive_actions = new List<Action>();
				List<Action> normal_actions = new List<Action>();
				Plan aggressive_plan;
				Plan normal_plan;
				
				if(regions_in_danger.Count == 0)
					continue;
					
				foreach(int region in regions_in_danger)
				{
					Action a = new Action();
					a.from = region;
					a.to = region;
					a.defend = true;
					a.existing_armies = MapClass.regions[region].number_of_armies - 1;
					normal_actions.Add(a);
				}
				
				List<double> success_by_units;
				List<int> units_allocation;
				compute_success_by_units(normal_actions, out success_by_units, out units_allocation);
				double enemy_chance_without_plan = 1.0 - success_by_units[0];
				double modifier = Planning.defend_bonus_modifier * enemy_chance_without_plan;

				normal_plan = create_plan(normal_actions, bonus, "defend bonus " + bonus, modifier);
				plans.Add(normal_plan);

				List<Tuple<int, int, int>> attacks_locations = attacking_plans(dangerous_regions);	//attacks sources and destinations
				aggressive_actions = generate_attacking_actions(attacks_locations);
				aggressive_plan = create_plan(aggressive_actions, bonus, "aggressively defend " + bonus, Planning.aggressive_defense_modifier * enemy_chance_without_plan);
				plans.Add(aggressive_plan);
			}
			return plans;
		}

		public static List<int> prerequisities()
		{	
			List<int> ok_bonuses = new List<int>();
			for(int bonus=1; bonus < MapClass.bonuses.Count; ++bonus)
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

				if(my_bonus)
					ok_bonuses.Add(bonus);
			}
			return ok_bonuses;
		}

		public static List<int> compute_regions_in_danger(int bonus)
		{
			List<int> regions_in_danger = new List<int>();
			foreach(int region_id in MapClass.bonuses[bonus].regions)
			{
				if(MapClass.enemy_neighbors(region_id).Count > 0)
					regions_in_danger.Add(region_id);
			}
			return regions_in_danger;
		}

		public static List<int> compute_dangerous_regions(int bonus)
		{
			List<int> dangerous = new List<int>();
			foreach(int region_id in MapClass.bonuses[bonus].regions)
			{
				if(MapClass.regions[region_id].owner == OwnerEnum.Me)
				{
					foreach(int dangerous_id in MapClass.enemy_neighbors(region_id))
					{
						if(!dangerous.Contains(dangerous_id))
							dangerous.Add(dangerous_id);
					}
				}
			}
			return dangerous;
		}
	}

	class SpoilBonusPlanGenerator : AbstractPlanGenerator
	{
		public override List<Plan> generate_plans()
		{
			List<Plan> plans = new List<Plan>();
			
			foreach(int bonus in prerequisities())
			{
				Action a = generate_action(bonus);
				List<Action> actions = new List<Action>();
				actions.Add(a);

				Plan p = create_plan(actions, bonus, "Spoil bonus " + bonus, Planning.spoil_bonus_modifier * KnowledgeBase.enemy_bonus(bonus));
				plans.Add(p);
			}
			return plans;
		}
		
		public List<int> prerequisities()
		{
			List<int> ok_bonuses = new List<int>();
			for(int bonus = 1; bonus < MapClass.bonuses.Count; ++bonus)
			{
				double enemies_bonus_probability = KnowledgeBase.enemy_bonus(bonus);
				bool ok_bonus = false;

				foreach(int region_id in MapClass.bonuses[bonus].regions)	
				{
					if(MapClass.regions[region_id].owner == OwnerEnum.Enemy)
						ok_bonus = true;
				}
				
				if((enemies_bonus_probability > 0) && (ok_bonus))
					ok_bonuses.Add(bonus);
			}
			return ok_bonuses;
		}
	}

	class ContinueSpoilingPlanGenerator : AbstractPlanGenerator
	{
		public override List<Plan> generate_plans()
		{
			List<Plan> plans = new List<Plan>();
			
			foreach(int bonus in prerequisities())
			{
				double ok_bonus_probability = 1.0;
				double enemy_successfull_attack_probability = 1;	//probability that enemy will successfully conquer whole bonus if I will not do anything, will be estimated later
				foreach(int region in MapClass.bonuses[bonus].regions)
				{
					if(MapClass.regions[region].owner == OwnerEnum.Unknown)
						ok_bonus_probability = ok_bonus_probability * KnowledgeBase.region_owner(region, OwnerEnum.Enemy);

					Action defense = new Action();		//this action is hypothetical, not part of the plan
					defense.from = region;
					defense.defend = true;
					defense.existing_armies = MapClass.regions[region].number_of_armies;
					enemy_successfull_attack_probability *= (1.0 - KnowledgeBase.compute_action_success(defense, 0));
				}
					
				Action a = generate_action(bonus);
				List<Action> actions = new List<Action>();
				actions.Add(a);

				Plan p = create_plan(actions, bonus, "Continue spoiling " + bonus, 
							ok_bonus_probability * enemy_successfull_attack_probability * Planning.continue_spoiling_modifier);
				
				plans.Add(p);
			}
			return plans;
		}

		public List<int> prerequisities()		//no neutral regions, one enemy's region, one mine region, enemy attack all regions in bonus
		{	
			List<int> ok_bonuses = new List<int>();
			for(int bonus=1; bonus<MapClass.bonuses.Count; ++bonus)
			{
				bool bonus_ok = true;
				bool exists_enemy_region = false;
				bool exists_my_region = false;

				foreach(int region in MapClass.bonuses[bonus].regions)
				{
					if(MapClass.regions[region].owner == OwnerEnum.Neutral)
					{
						bonus_ok = false;
						break;
					}
					else if((MapClass.regions[region].owner == OwnerEnum.Enemy) || (MapClass.regions[region].owner == OwnerEnum.Unknown))
						exists_enemy_region = true;

					else if(MapClass.regions[region].owner == OwnerEnum.Me)
					{
						exists_my_region = true;
						if(MapClass.enemy_neighbors(region).Count == 0)
							bonus_ok = false;
					}
				}

				if(bonus_ok && exists_enemy_region && exists_my_region)
					ok_bonuses.Add(bonus);
			}
			return ok_bonuses;
		}
	}

	class TakeRegionPlanGenerator : AbstractPlanGenerator
	{
		public override List<Plan> generate_plans()
		{
			List<Plan> plans = new List<Plan>();
			foreach(int bonus in prerequisities())
			{
				for(int number_of_actions = 1; number_of_actions <= MapClass.bonuses[bonus].regions.Count; ++number_of_actions)
				{
					List<int> regions_to_attack = compute_regions_to_attack(bonus, number_of_actions);
					if(regions_to_attack.Count < number_of_actions)
						continue;

					List<Tuple<int, int, int>> attack_locations = attacking_plans(regions_to_attack);
					List<Action> actions = generate_attacking_actions(attack_locations);
	
					int remaining_armies = compute_remaining_armies(bonus, regions_to_attack);
					double modifier;
					if(remaining_armies > 0)
						modifier = Planning.take_region_modifier / ((double)(remaining_armies * remaining_armies));
					else
						modifier = Planning.take_region_modifier;

					Plan p = create_plan(actions, bonus, "take " + number_of_actions + " regions in " + bonus, modifier);

					plans.Add(p);
				}
			}
			return plans;
		}

		public List<int> compute_regions_to_attack(int bonus, int number_of_actions)
		{
			List<int> enemy_regions = new List<int>();
			List<int> neutral_regions = new List<int>();
			List<int> result = new List<int>();
				
			foreach(int region in MapClass.bonuses[bonus].regions)
			{
				if(MapClass.regions[region].owner == OwnerEnum.Enemy)
					enemy_regions.Add(region);
				
				else if(MapClass.regions[region].owner == OwnerEnum.Neutral)
					neutral_regions.Add(region);
			}
				
			enemy_regions.Sort(delegate(int r1, int r2)
					{return MapClass.regions[r1].number_of_armies.CompareTo(MapClass.regions[r2].number_of_armies);});
			
			neutral_regions.Sort(delegate(int r1, int r2) 
					{return MapClass.regions[r1].number_of_armies.CompareTo(MapClass.regions[r2].number_of_armies);});

			if((enemy_regions.Count > 2) && 
				(MapClass.regions[enemy_regions[1]].number_of_armies < MapClass.regions[enemy_regions[0]].number_of_armies))
			{
				Utils.error_output("Error 14");		//badly sorted array, should be ascending
			}
			
			enemy_regions.AddRange(neutral_regions);

			for(int i=0; i<number_of_actions; ++i)		
			{
				if(i >= enemy_regions.Count)
					break;
				result.Add(enemy_regions[i]);
			}

			return result;
		}

		public int compute_remaining_armies(int bonus, List<int> regions_to_attack)
		{
			int armies = 0;
			foreach(int region in MapClass.bonuses[bonus].regions)
			{
				if((!regions_to_attack.Contains(region)) && (MapClass.regions[region].owner != OwnerEnum.Me))
					armies += KnowledgeBase.armies_in_region(region);
			}
			return armies;
		}

		public List<int> prerequisities()
		{
			List<int> ok_bonuses = new List<int>();
			for(int bonus=1; bonus<MapClass.bonuses.Count; ++bonus)
			{
				bool bonus_ok = false;
				foreach(int region in MapClass.bonuses[bonus].regions)
				{
					if(MapClass.regions[region].owner == OwnerEnum.Enemy)
						bonus_ok = true;
					
					else if(MapClass.regions[region].owner == OwnerEnum.Neutral)
						bonus_ok = true;
				}

				if(bonus_ok)
					ok_bonuses.Add(bonus);
			}
			return ok_bonuses;
		}
	}

	class Action
	{
		public bool defend;		//if true, than "to" to is empty
		public int from;		//for support of multiple attacks to one location
		public int to;
		public int existing_armies;
		public List<Action> supporting_actions = new List<Action>();

		public void check()
		{
			if((!defend) && ((to == 0) || (to >= MapClass.regions.Count)))
			{
				Utils.error_output("Error 25");
			}
			if((from == 0) || (from >= MapClass.regions.Count))
			{
				Utils.error_output("Error 16");
			}
			if(MapClass.regions[from].owner != OwnerEnum.Me)
			{
				Utils.error_output("Error 20");
			}
		}
	}

	class Plan
	{
		public int bonus_id;
		public List<Action> actions = new List<Action>();
		public List<int> units_allocation = new List<int>();				//which action should have n-th unit
		public List<Tuple<int, int>> needed_existing_armies = new List<Tuple<int, int>>();		//where and how many
		public List<double> profit = new List<double>();
		public string description;

		public void check_plan()
		{
			foreach(Action a in actions)
			{
				a.check();	
			}
			
			foreach(int i in units_allocation)
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
				p.units_allocation.Add(0);
				p.profit.Add(0);
			}
			return p;
		}
	}

	class KnowledgeBase
	{
		static public int armies_in_region(int region)
		{
			if(MapClass.regions[region].owner != OwnerEnum.Unknown)
				return MapClass.regions[region].number_of_armies;

			else if(region_owner(region, OwnerEnum.Enemy) > 0.5)
				return 1;		//just a guess
		
			else if(MapClass.regions[region].wasteland)
				return 10;		//10 armies in wasteland
			
			else
				return 2;		//2 armies in neutral region

			//Utils.error_output("Error 24");
			//return 2;
		}
		
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
		
		static public double enemy_recruit(int number_of_armies)	//enemy will recruit number_of_armies in given region
		{
			if(number_of_armies > enemy_income())
				return 0;

			double number_of_enemy_regions = 0;
			for(int region = 1; region < MapClass.regions.Count; ++region)
				number_of_enemy_regions += region_owner(region, OwnerEnum.Enemy);	

			int int_enemy_regions = (int)Math.Round(number_of_enemy_regions);
			
			double positive_possibilities = combination_number(enemy_income() - number_of_armies + int_enemy_regions - 2, enemy_income() - number_of_armies);
			double all_possibilities = combination_number(enemy_income() + int_enemy_regions - 1, enemy_income());

			double result = positive_possibilities / all_possibilities;

			if(result > 1)
			{
				Utils.error_output("Error 24");
				return 1.0;
			}

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

		static public double mean_attacker_losses(int defending)
		{
			if(attacker_losses_table.Count == 0)
			{
				attacker_losses_table = load_attacker_losses_table();
			}
			if(defending >= attacker_losses_table.Count)
			{
				return 0.7* (double)defending;
			}
			return attacker_losses_table[defending];
		}

		static public double mean_defender_losses(int attacking)
		{
			double sum = 0;
			for(int i=1; i<attacking; ++i)
			{
				sum += ((double)i) * (attack_success_table[attacking][i] - attack_success_table[attacking][i + 1]);
			}
			return sum;
		}

		static private double attack_with_recruitment(int me_attacking, int enemy_defending)
		{
			double p = 0;
			for(int i=0; i<enemy_income(); ++i)
			{
				p += enemy_recruit(i) * attack_success(me_attacking, enemy_defending + i);
			}
			if((p > 1.0) || (p < 0.0))
				Utils.error_output("Error 22");

			return p;
		}

		static private double defend_with_recruitment(int enemy_attacking, int me_defending)		//including possible enemy's recruitment
		{
			double p = 1.0;
			for(int i=0; i<enemy_income(); ++i)
			{
				p -= enemy_recruit(i) * attack_success(enemy_attacking + i, me_defending);
			}
			if((p > 1) || (p < 0))
				Utils.error_output("Error 19");

			return p;
		}

		static public double compute_action_success(Action a, int extra_armies)
		{
			if(a.defend)
			{
				foreach(Action supporting in a.supporting_actions)
				{
					if(supporting.to == a.from)
					{
						extra_armies += supporting.existing_armies;
					}
				}

				int number_of_attackers = 0;					

				foreach(int adjacent_enemy in MapClass.enemy_neighbors(a.from))
					number_of_attackers += MapClass.regions[adjacent_enemy].number_of_armies - 1;

				return defend_with_recruitment(number_of_attackers, a.existing_armies + extra_armies);
			}
			else if(!a.defend)
			{
				double destroyed_by_support = 0;
				foreach(Action supporting in a.supporting_actions)
				{
					if(supporting.to == a.to)
					{
						destroyed_by_support += mean_defender_losses(supporting.existing_armies);
					}
				}

				if(MapClass.regions[a.to].owner == OwnerEnum.Enemy)
					return attack_with_recruitment(a.existing_armies + extra_armies, 
									MapClass.regions[a.to].number_of_armies - (int)Math.Round(destroyed_by_support));
				else
					return attack_success(a.existing_armies + extra_armies, 
									MapClass.regions[a.to].number_of_armies - (int)Math.Round(destroyed_by_support));
			}
			Utils.error_output("Error 23");
			return 0;
		}

		static public double enemy_move(int from, int to, int number_of_armies)			//enemy will make move with number of armies
		{
			double possible_moves = MapClass.neighbors(from).Count + 1;
			double chance = 1.0 / possible_moves;
			int armies_to_recruit = number_of_armies - MapClass.regions[from].number_of_armies + 1;
			if(armies_to_recruit > 0)
				chance = chance * enemy_recruit(armies_to_recruit);
			return chance;
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

		private static List<double> load_attacker_losses_table()
		{
			List<double> result = new List<double>();
			for(int defenders=0; defenders<attacker_losses_table_size; ++defenders)
			{
				List<double> chances = armies_destroyed(defenders, true);
				chances.Add(0);
				double mean_destroyed = 0;

				for(int destroyed=0; destroyed<chances.Count - 1; ++destroyed)
				{
					mean_destroyed += ((double)destroyed) * (chances[destroyed] - chances[destroyed + 1]);
				}
				result.Add(mean_destroyed);
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
		const int attacker_losses_table_size = 20;
		private static List<List<double>> attack_success_table = load_attack_success_table();
		private static List<double> attacker_losses_table = load_attacker_losses_table(); //mean value of units destroyed when attacking "index" units
		private static List<double> enemy_regions = new List<double>();
		private static int enemy_income_number = 5;
	}
}









//#define FILE

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace warlight
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Bot b = new Bot();
			b.run();
		}
	}

	class Bot
	{
		public void run()
		{
			game_start();

			while(Utils.current_line.Length > 0)
			{
				if((Utils.current_line[0] == "settings") && (Utils.current_line[1] == "starting_armies"))
					Bot.set_number_of_armies_to_deploy();

				else if(Utils.current_line[0] == "update_map")
				{
					round_number++;
					Utils.error_output("ROUND " + round_number);
					MapClass.update();
					KnowledgeBase.update();
					Planning.decide_actions();
				}
				
				else if((Utils.current_line[0] == "go") && (Utils.current_line[1] == "place_armies"))
				{
					deploy_armies();
				}
				else if((Utils.current_line[0] == "go") && (Utils.current_line[1] == "attack/transfer"))
				{
					move_armies();
				}
				else if(Utils.current_line[0] == "opponent_moves")
				{

				}
				else
				{
					Utils.error_output("Error 8: ");
					StringBuilder line = new StringBuilder();
					foreach(string s in Utils.current_line)
					{	
						line.Append(s);
						line.Append(" ");
					}
					Utils.error_output(line.ToString());
				}
				Utils.next_line();
			}
		}

		public void deploy_armies()
		{
			for(int i=0; i<Planning.planned_deployments.Count; ++i)
			{
				Tuple<int, int> t = Planning.planned_deployments[i];
				if(i > 0)
					Console.Write(", ");
				Console.Write(GameSettings.my_name + " place_armies " + t.Item1 + " " + t.Item2);
			}
			Console.WriteLine();
		}

		public void move_armies()
		{	
			for(int i=0; i<Planning.planned_actions.Count; ++i)
			{
				Tuple<int, int, int> t = Planning.planned_actions[i];
				if(i > 0)
					Console.Write(", ");

				Console.Write(GameSettings.my_name + " attack/transfer " + t.Item1 + " " + t.Item2 + " " + t.Item3);
			}
			Console.WriteLine();
		}
	
		public void choose_starting_regions()
		{		
			List<int> regions_to_choose = new List<int>();
			for(int i=2; i<Utils.current_line.Length; ++i)
				regions_to_choose.Add(Int32.Parse(Utils.current_line[i]));

			List<int> bonuses_to_choose = new List<int>();
			foreach(int region in regions_to_choose)
			{
				for(int bonus = 1; bonus < MapClass.bonuses.Count; ++bonus)
				{
					if(MapClass.bonuses[bonus].regions.Contains(region))
					{
						bonuses_to_choose.Add(bonus);
						break;
					}
				}
			}

			int best_index = 0;
			double best_effectivity = 0;
			for(int i=0; i<bonuses_to_choose.Count; ++i)
			{
				int bonus = bonuses_to_choose[i];

				int sum = 0;
				foreach(int region in MapClass.bonuses[bonus].regions)
				{
					if(MapClass.regions[region].wasteland)
						sum += 10;
					
					else 
						sum += 2;
				}
				double effectivity = ((double)MapClass.bonuses[bonus].bonus_income) / ((double)sum);
				
				Utils.error_output("region " + regions_to_choose[i] + " effectivity = " + effectivity);
				
				if((effectivity > best_effectivity) || ((effectivity == best_effectivity) 
						&& (MapClass.bonuses[bonus].regions.Count < MapClass.bonuses[bonuses_to_choose[best_index]].regions.Count)))
				{
					best_effectivity = effectivity;
					best_index = i;
					Utils.error_output("best region: " + regions_to_choose[best_index]);
				}
			}
			
			int chosen = regions_to_choose[best_index];
			Console.WriteLine(chosen);	
			Utils.error_output("chosen region: " + chosen);

			MapClass.regions[chosen].owner = OwnerEnum.Me;
			MapClass.regions[chosen].number_of_armies = 2;
		}

		public void game_start()
		{
			while(true)
			{
				Utils.next_line();
				
				if(Utils.current_line[0] == "pick_starting_region")
					choose_starting_regions();

				else if(Utils.current_line[0] == "settings")
					GameSettings.load();

				else if(Utils.current_line[0] == "setup_map")
					MapClass.load();

				else
				{
					Utils.error_output("End of game start");
					break;
				}
			}
		}

		public static void set_number_of_armies_to_deploy()
		{
			armies_to_deploy = Int32.Parse(Utils.current_line[2]);
		}
		
		static int round_number = 0;
		static public int armies_to_deploy;
		static public List<int> previous_starting_regions;
	}

	class GameSettings
	{
		public static void load()
		{
			if((Utils.current_line[0] == "settings") && (Utils.current_line[1] == "your_bot"))
				my_name = Utils.current_line[2];

			else if((Utils.current_line[0] == "settings") && (Utils.current_line[1] == "opponent_bot"))
				enemy_name = Utils.current_line[2];

			else if((Utils.current_line[0] == "settings") && (Utils.current_line[1] == "starting_regions"))
			{
				for(int i=2; i<Utils.current_line.Length; ++i)
					starting_regions.Add(Int32.Parse(Utils.current_line[i]));

				Bot.previous_starting_regions = starting_regions;
				if(starting_regions.Count % 2 == 1)
					odd_number_of_starting_regions = true;
				else
					odd_number_of_starting_regions = false;
			}
			else if((Utils.current_line[0] == "settings") && (Utils.current_line[1] == "starting_armies"))
			{
				Bot.set_number_of_armies_to_deploy();
			}
		}

		public static string my_name;
		public static string enemy_name;
		public static List<int> starting_regions = new List<int>();
		public static bool odd_number_of_starting_regions;
	}

	class MapClass
	{
		public static void load()
		{
			if(Utils.current_line[0] != "setup_map")
			{
				Utils.error_output("Error 2: " + Utils.current_line[0]);
				return;
			}

			if(Utils.current_line[1] == "super_regions")
			{
				bonuses.Add(new BonusClass());
				bonuses[bonuses.Count - 1].bonus_income = -1;

				for(int i=1; i<Utils.current_line.Length/2; i++)
				{
					bonuses.Add(new BonusClass());
					bonuses[bonuses.Count - 1].bonus_income = Int32.Parse(Utils.current_line[i*2 + 1]);
					
					if(Int32.Parse(Utils.current_line[i*2]) != i)
						Utils.error_output("Error 3" + Utils.current_line[i*2]);	//Superregions should have numbers from 1 to number of superregions
				}
			}

			else if(Utils.current_line[1] == "regions")
			{
				regions.Add(new Region());
				regions[regions.Count - 1].bonus = bonuses[0];
				regions[regions.Count - 1].owner = OwnerEnum.Unknown;
				regions[regions.Count - 1].number_of_armies = -1;
				regions[regions.Count - 1].wasteland = false;

				for(int i=1; i<Utils.current_line.Length/2; ++i)
				{
					regions.Add(new Region());
					if(bonuses.Count <= Int32.Parse(Utils.current_line[i*2 + 1]))
						Utils.error_output("Error 4" + Utils.current_line[i*2 + 1]);
					
					else
					{
						int bonus_id = Int32.Parse(Utils.current_line[i*2 + 1]);
						regions[regions.Count - 1].bonus = bonuses[bonus_id];
						bonuses[bonus_id].regions.Add(i);
						regions[regions.Count - 1].owner = OwnerEnum.Neutral;
						regions[regions.Count - 1].number_of_armies = 2;
						regions[regions.Count - 1].wasteland = false;
					}
				}
				
				for(int i=0; i<regions.Count; ++i)			//one filler region on index 0
				{
					distances.Add(new List<int>());
					for(int j=0; j<regions.Count; ++j)
					{
						distances[i].Add(1000000);		//1000000 is approximately infinity, for floyd warshall

						if(i == j)
							distances[i][j] = 0;
					}
				}
			}

			else if(Utils.current_line[1] == "neighbors")
			{
				if(distances.Count == 0)
				{
					Utils.error_output("Error 5 - neighbors before regions");
					return;
				}

				for(int i=1; i<Utils.current_line.Length/2; i++)
				{
					int first = Int32.Parse(Utils.current_line[i*2]);
					string[] neighbors = Utils.current_line[i*2 + 1].Split(',');
					for(int j=0; j<neighbors.Length; ++j)
					{
						int second = Int32.Parse(neighbors[j]);
						distances[first][second] = 1;
						distances[second][first] = 1;
					}
				}
				compute_distances();
			}
			
			else if(Utils.current_line[1] == "wastelands")
			{
				for(int i=2; i<Utils.current_line.Length; ++i)
				{
					int region_id = Int32.Parse(Utils.current_line[i]);
					if(region_id >= regions.Count)
						Utils.error_output("Error 7");
					else
					{
						region_id = Int32.Parse(Utils.current_line[i]);
						regions[region_id].number_of_armies = 10;
						regions[region_id].wasteland = true;
					}
				}
			}

			else if(Utils.current_line[1] == "opponent_starting_regions")
			{
				for(int i=2; i<Utils.current_line.Length; ++i)
				{
					int region_id = Int32.Parse(Utils.current_line[i]);
					KnowledgeBase.enemy_starting_regions(region_id);
				}
			}
		}

		public static void update()
		{
			if(Utils.current_line[0] == "update_map")
			{
				for(int i=1; i < regions.Count; ++i)
				{
					regions[i].owner = OwnerEnum.Unknown;
					regions[i].number_of_armies = 0;
				}

				for(int i=1; i <= Utils.current_line.Length/3; ++i)
				{
					int region_id = Int32.Parse(Utils.current_line[i*3 - 2]);
					
					OwnerEnum owner = OwnerEnum.Unknown;
					if(Utils.current_line[3*i - 1] == GameSettings.my_name)
						owner = OwnerEnum.Me;
					
					else if(Utils.current_line[3*i - 1] == GameSettings.enemy_name)
						owner = OwnerEnum.Enemy;

					else if(Utils.current_line[3*i - 1] == "neutral")
						owner = OwnerEnum.Neutral;

					else 
						Utils.error_output("Error 9: " + Utils.current_line[3*i - 1]);

					int number_of_armies = Int32.Parse(Utils.current_line[i*3]);

					MapClass.regions[region_id].owner = owner;
					MapClass.regions[region_id].number_of_armies = number_of_armies;
				}
			}
			else
				Utils.error_output("Error 10: " + Utils.current_line[0]);
		}

		public static void compute_distances()		//floyd-warshall algorithm
		{
			for(int k=1; k<distances.Count; ++k)
			{
				for(int i=1; i<distances.Count; ++i)
				{
					for(int j=1; j<distances.Count; ++j)
					{
						if(distances[i][j] > distances[i][k] + distances[k][j]) 
						{
							distances[i][j] = distances[i][k] + distances[k][j];
							distances[j][i] = distances[i][j];
						}
					}
				}
			}
		}

		public static List<int> neighbors(int region)
		{
			List<int> l = new List<int>();
			for(int i=1; i<distances.Count; ++i)
			{
				if(distances[region][i] == 1)
					l.Add(i);
			}
			return l;
		}

		public static List<int> neighbors_by_predicate(int region, Func<int, bool> predicate)
		{
			List<int> l = new List<int>();
			foreach(int neighbor in neighbors(region))
			{
				if(predicate(neighbor))
					l.Add(neighbor);
			}
			return l;
		}
		
		public static List<int> my_neighbors(int region)
		{
			return neighbors_by_predicate(region, r => MapClass.regions[r].owner == OwnerEnum.Me);
		}
			
		public static List<int> enemy_neighbors(int region)
		{
			return neighbors_by_predicate(region, r => MapClass.regions[r].owner == OwnerEnum.Enemy);
		}
		
		public static int path_to_enemy(int start) 		//returns start if enemy is adjacent, else returns first region of path to some enemy
		{							//if no path can be found, return -1
			return bfs(start, r => enemy_neighbors(r).Count > 0);
		}

		public static int path_to_neutral(int start)		//similar output as path_to_enemy()
		{
			return bfs(start, r => my_neighbors(r).Count > 0);
		}

		public static int bfs(int start, Func<int, bool> goal_test)
		{
			if(goal_test(start))
				return start;

			List<Tuple<int, int>> frontier = new List<Tuple<int, int>>();		//current region, first region of path
			List<bool> explored = new List<bool>();
			for(int i=0; i<MapClass.regions.Count; ++i)
			{
				explored.Add(false);
			}
			explored[start] = true;

			foreach(int my_neighbor in MapClass.my_neighbors(start))
			{
				frontier.Add(new Tuple<int, int>(my_neighbor, my_neighbor));
				explored[my_neighbor] = true;
			}
			
			int index = 0;

			while(frontier.Count > index)
			{
				if(goal_test(frontier[index].Item1))	//found path to enemy
					return frontier[index].Item2;

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
			return -1;
		}

		public static List<Tuple<int, int>> existing_armies()
		{
			List<Tuple<int, int>> armies = new List<Tuple<int, int>>();
			for(int i=1; i<regions.Count; ++i)
			{
				if((regions[i].owner == OwnerEnum.Me) && (regions[i].number_of_armies > 1))
				{
					armies.Add(new Tuple<int, int>(i, regions[i].number_of_armies - 1));
				}
			}
			return armies;
		}

		public static List<int> free_armies_by_region()
		{
			List<int> armies = new List<int>();
			for(int i=1; i<MapClass.regions.Count; ++i)
			{
				if(MapClass.regions[i].owner == OwnerEnum.Me)
					armies.Add(MapClass.regions[i].number_of_armies - 1);
				
				else
					armies.Add(0);
			}
			return armies;
		}

		public static List<Region> regions = new List<Region>();		//region n is on n-th index, region on index 0 is just a filler 
		public static List<BonusClass> bonuses = new List<BonusClass>();	//bonus on index 0 is just a filler
		public static List<List<int>> distances = new List<List<int>>();		//0th row and 0th column are fillers
	}

	enum OwnerEnum {Me, Enemy, Neutral, Unknown};
	

	class Region
	{
		public OwnerEnum owner;
		public int number_of_armies;		//0 means unknown
		public BonusClass bonus;
		public bool wasteland;
	}

	class BonusClass
	{
		public List<int> regions = new List<int>();
		public int bonus_income;
	}

	class Utils
	{
		public static void error_output(string text)
		{
			error_writer.WriteLine(text);
		}

		public static void next_line()
		{
		
			string s;
			#if FILE
			s = reader.ReadLine();
			#else
			s = Console.ReadLine();
			#endif
		
			if(s == null)
			{
				Utils.error_output("End of input");
				Environment.Exit(0);
			}
			char[] separators = new char[] {' ', '\t', '\n'};
			Utils.current_line = s.Split(separators, StringSplitOptions.RemoveEmptyEntries);
		}
		
		public static Random rnd = new Random(11);
		public static string[] current_line;
		private static TextWriter error_writer = Console.Error;

		#if FILE
		public static StreamReader reader = new StreamReader("/home/beda/c_sharp/warlight/test4");
		#endif
	}
}

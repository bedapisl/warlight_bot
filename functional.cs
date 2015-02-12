using System;
using System.Collections.Generic;

namespace warlight
{
	class Functional
	{
		public static List<OUT> map<IN, OUT>(List<IN> l, Func<IN, OUT> f)
		{
			List<OUT> result = new List<OUT>();
			foreach(IN element in l)
				result.Add(f(element));

			return result;
		}

		public static List<Tuple<int, int, int>> maximal_flow(List<Tuple<int, int>> first_partity, List<Tuple<int, int>> second_partity, List<Tuple<int, int>> edges)
		{
			List<Tuple<int, int, int>> flow = map<Tuple<int, int>, Tuple<int, int, int>>(edges, (t => new Tuple<int, int, int>(t.Item1, t.Item2, 0)));
			
			List<int> improving_path = new List<int>();
			int improving_size = compute_improving_path(first_partity, second_partity, flow, ref improving_path, 100000, true);

			Utils.error_output("computing maximal flow");

			while(improving_size > 0)
			{
				bool forward = true;
				for(int i=0; i<improving_path.Count - 1; ++i)
				{
					if(forward)
					{
						for(int j=0; j<flow.Count; ++j)
						{
							if((flow[j].Item1 == improving_path[i]) && (flow[j].Item2 == improving_path[i+1]))
							{
								flow[j] = new Tuple<int, int, int>(flow[j].Item1, flow[j].Item2, flow[j].Item3 + improving_size);
								break;
							}
						}
					}
					if(!forward)
					{
						for(int j=0; j<flow.Count; ++j)
						{
							if((flow[j].Item2 == improving_path[i]) && (flow[j].Item1 == improving_path[i + 1]))
							{
								flow[j] = new Tuple<int, int, int>(flow[j].Item1, flow[j].Item2, flow[j].Item3 - improving_size);
								break;
							}
						}
					}
					forward = !forward;
				}
				improving_path.Clear();
				improving_size = compute_improving_path(first_partity, second_partity, flow, ref improving_path, 1000000, true);
			}

			Utils.error_output("end of computation");
			return flow;
		}

		public static int compute_improving_path(List<Tuple<int, int>> first_partity, List<Tuple<int, int>> second_partity, List<Tuple<int, int, int>> flow,
								ref List<int> improving_path, int improving_size, bool first)
		{
			if(improving_path.Count == 0)
			{
				foreach(Tuple<int, int> t in first_partity)
				{
					int increase = t.Item2;		//how much more can flow to vertex t
					foreach(Tuple<int, int, int> flow_edge in flow)
					{
						if(t.Item1 == flow_edge.Item1)
							increase -= flow_edge.Item3;
					}

					if(increase == 0)
						continue;

					improving_path.Add(t.Item1);
					int improvement = compute_improving_path(first_partity, second_partity, flow, ref improving_path, increase, false);
					if(improvement > 0)
						return improvement;
					
					improving_path.Remove(t.Item1);
				}
				return 0;
			}

			if(first)
			{
				foreach(Tuple<int, int, int> t in flow)
				{
					if((t.Item1 == improving_path[improving_path.Count - 1]) && (!improving_path.Contains(t.Item2)))
					{
						int new_improving_size = Math.Min(improving_size, t.Item3);
						if(new_improving_size > 0)
						{
							improving_path.Add(t.Item1);
							int improvement = compute_improving_path(first_partity, second_partity, flow, ref improving_path, new_improving_size, false);
							if(improvement > 0)
								return improvement;

							improving_path.Remove(t.Item1);
						}
					}
				}
			}
			else
			{
				foreach(Tuple<int, int, int> t in flow)
				{
					if((t.Item1 == improving_path[improving_path.Count - 1]) && (!improving_path.Contains(t.Item2)))
					{
						improving_path.Add(t.Item2);
						int flowing = 0;
						foreach(Tuple<int, int, int> flow_edge in flow)
						{
							if(flow_edge.Item2 == t.Item2)
								flowing += flow_edge.Item3; 
						}
						foreach(Tuple<int, int> second_partity_vertex in second_partity)
						{
							if(t.Item2 == second_partity_vertex.Item1)
							{
								if(flowing < second_partity_vertex.Item2)
								{
									return Math.Min(second_partity_vertex.Item2 - flowing, improving_size);
								}
								break;
							}
						}

						int improvement = compute_improving_path(first_partity, second_partity, flow, ref improving_path, improving_size, true);
						if(improvement > 0)
							return improvement;

						improving_path.Remove(t.Item2);
					}
				}
			}
			return 0;
		}
	}
}
		















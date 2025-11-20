using static System.Console;

namespace Y0KAI_CDS
{
    internal class CDS(Graph g)
    {
        private class VJobData
        {
            public required string V_ID {  get; set; }
            public required double V_TM1 { get; set; }
            public required double V_TM2 { get; set; }
        }
        
        public List<string> Solve(bool isSilent)
        {
            List<string> baseJobIDs = [.. (from gk in g.Nodes.Keys 
                                           where gk.EndsWith("_M1") 
                                           select gk[..^3])
                                           .Distinct()];

            List<VJobData> j4j = [];

            foreach (string id in baseJobIDs)
            {
                double
                    tM1 = g.Nodes.TryGetValue($"{id}_M1", out Node? m1Node) ? m1Node.TimeM1 : 0d,
                    tM2 = g.Nodes.TryGetValue($"{id}_M2", out Node? m2Node) ? m2Node.TimeM2 : 0d;

                if (tM1 > 0 || tM2 > 0)
                    j4j.Add(new()
                    {
                        V_ID = id,
                        V_TM1 = tM1,
                        V_TM2 = tM2
                    });
            }

            if (j4j.Count is 0 && !isSilent)
                Error.WriteLine("Error: No valid jobs found for scheduling.");
           
            return j4j.Count is not 0 ? RunJohnson(j4j) : [];
        }

        private static List<string> RunJohnson(List<VJobData> jobs)
        {
            List<VJobData>
                s1 = [],
                s2 = [];

            foreach (VJobData job in jobs)
                if (job.V_TM1 <= job.V_TM2) s1.Add(job);
                else s2.Add(job);

            s1.Sort((a,b) => a.V_TM1.CompareTo(b.V_TM1));
            s2.Sort((a,b) => b.V_TM2.CompareTo(a.V_TM2));

            return [.. s1.Select(j => j.V_ID), .. s2.Select(j => j.V_ID)];
        }
    }
}

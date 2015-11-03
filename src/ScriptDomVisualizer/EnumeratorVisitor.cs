using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace ScriptDomVisualizer
{
    public class EnumeratorVisitor : TSqlFragmentVisitor
    {
        public List<TSqlStatement> Nodes = new List<TSqlStatement>(); 

        public override void Visit(TSqlStatement node)
        {
            base.Visit(node);

            if(!Nodes.Any(p=>p.StartOffset <= node.StartOffset && p.StartOffset + p.FragmentLength >= node.StartOffset + node.FragmentLength))
                Nodes.Add(node);

        }
    }
}

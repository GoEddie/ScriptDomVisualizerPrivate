using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace ScriptDomVisualizer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static Assembly ScriptDom;

        static MainWindow()
        {
            ScriptDom = Assembly.Load("Microsoft.SqlServer.TransactSql.ScriptDom");
        }

        public MainWindow()
        {
            InitializeComponent();
            
            Results.SelectedItemChanged += Results_SelectedItemChanged;
            Tokens.SelectedItemChanged += Tokens_SelectedItemChanged;
        }

        private void Tokens_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var item = (e.NewValue as TreeViewItem);
            if (item == null)
                return;

            var token = item.Tag as TSqlParserToken;

            if (null == token)
                return;

            _userChanges = false;
            var currentRange = InputBox.Selection;
            currentRange.Select(currentRange.Start.DocumentStart, currentRange.Start.DocumentEnd);
            currentRange.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Colors.Black));

            //if (token.of== -1 || fragment.FragmentLength == -1)
            //    return;

            try
            {

                currentRange = InputBox.Selection;


                var start = GetPoint(InputBox.Document.ContentStart, token.Offset);
                var end = GetPoint(InputBox.Document.ContentStart, token.Offset + token.Text.Length);
                currentRange.Select(start, end);

                var t = currentRange.Text;

                currentRange.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Colors.Blue));
            }
            catch (Exception esss) { Console.WriteLine(esss); }
            _userChanges = true;


        }

        private void Results_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var item = (e.NewValue as TreeViewItem);
            if (item == null)
                return;


            var fragment = TryGetTag(item);

            if (null == fragment)
                return;

            _userChanges = false;
            var currentRange = InputBox.Selection;
            currentRange.Select(currentRange.Start.DocumentStart, currentRange.Start.DocumentEnd);
            currentRange.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Colors.Black));

            if (fragment.StartOffset == -1 || fragment.FragmentLength == -1)
                return;

            try
            {
               
                currentRange = InputBox.Selection;
                

                var start = GetPoint(InputBox.Document.ContentStart, fragment.StartOffset);
                var end = GetPoint(InputBox.Document.ContentStart, fragment.FragmentLength + fragment.StartOffset);
                currentRange.Select(start, end);

                var t = currentRange.Text;

                currentRange.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Colors.Blue));
            }catch(Exception esss) { Console.WriteLine(esss); }
            _userChanges = true;
        }

        private static TextPointer GetPoint(TextPointer start, int x)
        {
            var ret = start;
            var i = 0;
            while (ret != null)
            {
                string stringSoFar = new TextRange(ret, ret.GetPositionAtOffset(i, LogicalDirection.Forward)).Text;
                if (stringSoFar.Length == x)
                    break;
                i++;
                if (ret.GetPositionAtOffset(i, LogicalDirection.Forward) == null)
                    return ret.GetPositionAtOffset(i - 1, LogicalDirection.Forward);

            }
            ret = ret.GetPositionAtOffset(i, LogicalDirection.Forward);
            return ret;
        }

        public TreeViewItem GetSelectedTreeViewItemParent(TreeViewItem item)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(item);
            while (!(parent is TreeViewItem || parent is TreeView))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            return parent as TreeViewItem;
        }

        private TSqlFragment TryGetTag(TreeViewItem item)
        {
            if (item.Tag is TSqlFragment)
            {
                return item.Tag as TSqlFragment;
            }


            var node = item;
            while (node.Parent != null)
            {
                if (node.Tag is TSqlFragment)
                    return node.Tag as TSqlFragment;

                node = GetSelectedTreeViewItemParent(node);
            }

            return null;
        }

        bool _userChanges = true;

        
        private void Parse()
        {
            Errors.Text = "";
            MaxDepth = Int32.Parse(DepthText.Text);

            var parser = new TSql120Parser(true);

            IList<ParseError> errors;
            var script = parser.Parse(new StringReader(GetText()), out errors);

            if (errors.Count > 0)
            {

                Errors.Text = "";
                foreach (var e in errors)
                {
                    Errors.Text += "Error: " + e.Message + " at: " + e.Offset + "\r\n";
                }


                return;
            }
            var enumerator = new EnumeratorVisitor();
            script.Accept(enumerator);
            Results.Items.Clear();

            foreach (var node in enumerator.Nodes)
            {
             
                foreach (var i in GetChildren(node))
                {
                    Results.Items.Add(i);
                }
            }

            Tokens.Items.Clear();
            var newItem = new TreeViewItem();
            newItem.Header = "Tokens";
            newItem.IsExpanded = true;

            foreach (var t in script.ScriptTokenStream)
            {
                var newChild = new TreeViewItem();
                newChild.Header = string.Format("{0} : {1} : {2} : {3}", t.TokenType, t.Text, t.Offset, t.Column);
                newItem.Items.Add(newChild);
                newChild.Tag = t;
            }

            Tokens.Items.Add(newItem);

        }

        private string GetText()
        {
            return new TextRange(InputBox.Document.ContentStart, InputBox.Document.ContentEnd).Text;
        }

        private int MaxDepth = 10;

        private List<TreeViewItem> GetChildren(object node, int depth = 0)
        {
            var items = new List<TreeViewItem>();

            if (depth++ > MaxDepth || IgnoreType(node))
                return items;

            if (node is IEnumerable<object>)
            {
                var collectionNode = new TreeViewItem();
                collectionNode.Header = TidyTypeName(node.GetType().FullName) + " + COLLECTION";
                collectionNode.Tag = GetTag(node as TSqlFragment);
                foreach (var child in node as IEnumerable<object>)
                {
                    var children = GetChildren(child, depth);
                    foreach (var c in children)
                    {
                        collectionNode.Items.Add(c);
                    }
                }
                items.Add(collectionNode);
                return items;
            }

            var nodeType = node.ToString().Split(' ')[0];
            var t = ScriptDom.GetType(nodeType, false, true);

            if (t == null)
            {
                var item = new TreeViewItem();
                item.Header = TidyTypeName(node.GetType().FullName) + " : " + node.ToString();
                items.Add(item);
                return items;
            }
            var newItem = new TreeViewItem();
            newItem.Header = TidyTypeName(node.GetType().FullName);
            newItem.Tag = GetTag(node as TSqlFragment);

            foreach (var p in t.GetProperties())
            {
                //ret += p.Name + " : " + p.GetValue(node) + " : " + p.GetType();

                var item = new TreeViewItem();
                item.Header = TidyTypeName(p.Name) + " : " + TidyTypeName(p.GetType().FullName) + " = " + TryGetValue(p, node);
                item.Tag =  GetTag(TryGetValue(p, node) as TSqlFragment);
                newItem.Items.Add(item);
                switch (p.Name)
                {
                    case "ScriptTokenStream":
                        break;

                    default:
                        //ret += "\t\t" + GetChildren(p, depth) + "\r\n";
                        foreach (var i in GetChildren( TryGetValue(p, node), depth))
                        {
                            item.Items.Add(i);
                        }

                        break;
                }

                
            }


            items.Add(newItem);
           return items;

        }

        private TSqlFragment GetTag(TSqlFragment sqlFragment)
        {
            return sqlFragment;
        }

        private object TryGetValue(PropertyInfo propertyInfo, object node)
        {
            try
            {
                return propertyInfo.GetValue(node);
            }
            catch (Exception)
            {
                return "";
            }
        }

        private string TidyTypeName(string fullName)
        {
            return
                fullName.Replace("Microsoft.SqlServer.TransactSql.ScriptDom.", "")
                    .Replace("System.Collections.Generic.List`1[[", "List<")
                    .Replace("System.Reflection.RuntimePropertyInfo", "");
        }

        private bool IgnoreType(object node)
        {
            if (node == null)
                return true;

            
            var type = node.GetType();
            Console.WriteLine(type);

            if (node.ToString().Contains("Microsoft.SqlServer.TransactSql.ScriptDom"))
            {
                return false;
            }

            return  !type.FullName.Contains("Microsoft.SqlServer.TransactSql.ScriptDom");
        }

        private void UIElement_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                Parse();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}

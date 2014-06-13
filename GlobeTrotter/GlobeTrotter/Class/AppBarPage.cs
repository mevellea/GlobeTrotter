using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace GlobeTrotter
{
    public class AppBarPage
    {
        private Page rootPage;
        private StackPanel leftPanel;
        private StackPanel rightPanel;
        private List<UIElement> leftItems;
        private List<UIElement> rightItems;

        public enum SIDE
        {
            LEFT,
            RIGHT,
            BOTH
        }

        public AppBarPage(Page _page, SIDE _side, EventHandler<object> _openedCallback)
        {
            rootPage = _page;
            leftItems = new List<UIElement>();
            rightItems = new List<UIElement>();

            rootPage.BottomAppBar.Opened += _openedCallback;

            leftPanel = rootPage.FindName("LeftPanel") as StackPanel;
            rightPanel = rootPage.FindName("RightPanel") as StackPanel;

            CopyButtons(leftPanel, leftItems);
            CopyButtons(rightPanel, rightItems);

            ClearAll();
            Update(_side);
        }

        private void ClearAll()
        {
            leftPanel.Children.Clear();
            rightPanel.Children.Clear();
        }

        private void Update(SIDE _side)
        {
            if (_side == SIDE.LEFT)
                RestoreButtons(leftPanel, leftItems);
            else if (_side == SIDE.RIGHT)
                RestoreButtons(rightPanel, rightItems);
            else if (_side == SIDE.BOTH)
            {
                RestoreButtons(leftPanel, leftItems);
                RestoreButtons(rightPanel, rightItems);
            }
        }

        private void CopyButtons(StackPanel panel, List<UIElement> list)
        {
            foreach (UIElement element in panel.Children)
                list.Add(element);
        }

        private void RestoreButtons(StackPanel panel, List<UIElement> list)
        {
            foreach (UIElement element in list)
                panel.Children.Add(element);
        }
        
        public void Show(SIDE _side)
        {
            ClearAll();
            rootPage.BottomAppBar.IsOpen = true;
            Update(_side);
        }

        public void Hide(SIDE _side)
        {
            ClearAll();
            rootPage.BottomAppBar.IsOpen = false;
            Update(_side);
        }

        public Boolean Opened()
        {
            return rootPage.BottomAppBar.IsOpen;
        }
    }
}

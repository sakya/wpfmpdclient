//    WpfMpdClient
//    Copyright (C) 2012, 2013 Paolo Iommarini
//    sakya_tg@yahoo.it
//
//    This program is free software; you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation; either version 2 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program; if not, write to the Free Software
//    Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Xml.Serialization;
using System.Collections.Specialized;

namespace WpfMpdClient
{
  public class ConfigurableListView : ListView
  {
    public class ColumnInfo
    {
      [XmlAttribute("Header")]
      public string Header
      {
        get;
        set;
      }

      [XmlAttribute("Width")]
      public double Width
      {
        get;
        set;
      }

      [XmlAttribute("IsVisible")]
      public bool IsVisible
      {
        get;
        set;
      }
    }

    ContextMenu m_ConextMenu = null;
    bool m_Initialized = false;

    public GridView GetView()
    {
      return View as GridView;
    }

    protected override void OnInitialized(EventArgs e)
    {
      SetListViewContextMenu();
      GridView view = GetView();
      if (view != null)
        view.Columns.CollectionChanged += ColumnsChangedHandler;
      base.OnInitialized(e);
      m_Initialized = true;
    }

    private void SetListViewContextMenu()
    {
      m_ConextMenu = new System.Windows.Controls.ContextMenu();
      m_ConextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;

      GridView view = GetView();
      foreach (GridViewColumn col in view.Columns){
        GridViewColumnHeader header = col.Header as GridViewColumnHeader;
        if (header != null){
          header.MouseRightButtonUp += HeaderRightByttonUpHandler;

          MenuItem item = new MenuItem() { Header = header.Content,
                                           IsCheckable = true,
                                           IsChecked = true };
          item.Checked += CheckedChangedHandler;
          item.Unchecked += CheckedChangedHandler;
          item.Tag = col;

          m_ConextMenu.Items.Add(item);                                              
        }
      }
    }

    private void HeaderRightByttonUpHandler(object sender, MouseButtonEventArgs e)
    {
      m_ConextMenu.IsOpen = !m_ConextMenu.IsOpen;
    }

    private void ColumnsChangedHandler(object sender, NotifyCollectionChangedEventArgs e)
    {
      if (e.Action == NotifyCollectionChangedAction.Move){
        MenuItem item = m_ConextMenu.Items[e.OldStartingIndex] as MenuItem;
        m_ConextMenu.Items.Remove(item);
        m_ConextMenu.Items.Insert(e.NewStartingIndex, item);
      }
    }

    private void CheckedChangedHandler(object sender, RoutedEventArgs e)
    {
      MenuItem item = sender as MenuItem;
      if (item != null){
        GridView view = GetView();
        GridViewColumn col = item.Tag as GridViewColumn;
        if (col != null){
          if (item.IsChecked){
            int index = m_ConextMenu.Items.IndexOf(item);
            if (index >= 0 && index < view.Columns.Count)
              view.Columns.Insert(index, col);
            else
              view.Columns.Add(col);
          }else
            view.Columns.Remove(col);
        }
      }
    }

    public List<ColumnInfo> GetColumnsInfo()
    {
      List<ColumnInfo> info = new List<ColumnInfo>();

      GridView view = GetView();
      foreach (MenuItem item in m_ConextMenu.Items){
        GridViewColumn col = item.Tag as GridViewColumn;
        ColumnInfo i = new ColumnInfo() { Header = item.Header as string,
                                          Width = col.ActualWidth,
                                          IsVisible = item.IsChecked };

        info.Add(i);
      }

      return info;
    }

    public void SetColumnsInfo(List<ColumnInfo> info)
    {
      if (m_Initialized && info != null && info.Count > 0){
        GridView view = GetView();
        List<MenuItem> orderedItems = new List<MenuItem>();
        view.Columns.Clear();
        foreach (ColumnInfo i in info){
          MenuItem item = GetItem(i.Header);
          if (item != null){
            GridViewColumn col = item.Tag as GridViewColumn;
            col.Width = i.Width;               
            item.IsChecked = i.IsVisible;
            if (item.IsChecked)
              view.Columns.Add(col);
            orderedItems.Add(item);
          }
        }

        m_ConextMenu.Items.Clear();
        foreach (MenuItem item in orderedItems)
          m_ConextMenu.Items.Add(item);
      }
    }

    private MenuItem GetItem(string header)
    {
      foreach (MenuItem item in m_ConextMenu.Items){
        if (item.Header as string == header)
          return item; 
      }
      return null;
    }

  }
}

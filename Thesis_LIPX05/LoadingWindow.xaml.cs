﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Thesis_LIPX05
{
    /// <summary>
    /// Interaction logic for LoadingWindow.xaml
    /// </summary>
    public partial class LoadingWindow : Window
    {
        public LoadingWindow()
        {
            InitializeComponent();
        }

        public async Task UpdateProgressAsync(string status)
        {
            if (Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(() => UpdateProgress(status));
                return;
            }
        }

        private void UpdateProgress(string status) => LoadingText.Text = status;
    }
}

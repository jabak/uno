﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SamplesApp
{
	public sealed partial class MyControl : UserControl
	{
		private static int _count;

		private int InstanceId { get; } = _count++;

		public MyControl()
		{
			this.InitializeComponent();
		}

		private Brush GetColor(object value)
		{
			if (int.TryParse(value?.ToString(), out var id))
			{
				switch (id % 6)
				{
					case 0: return new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x00, 0x00));
					case 1: return new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x7F, 0x00));
					case 2: return new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0x00));
					case 3: return new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xFF, 0x00));
					case 4: return new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0xFF));
					case 5: return new SolidColorBrush(Color.FromArgb(0xFF, 0x94, 0x00, 0xD3));
				}
			}

			return new SolidColorBrush(Colors.Black);
		}
	}
}
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace LargeFileProxy
{
	public partial class MainWindow : Window
	{
		private readonly FileProxy _container;

		public MainWindow()
		{
			InitializeComponent();
			this.Loaded += OnLoaded;

			_container = new FileProxy(Path.Combine(Path.GetTempPath(), "Test.txt"));
		}

		private async void OnLoaded(object sender, RoutedEventArgs e)
		{
			await _container.InitializeAsync();
		}

		private async void AddButton_Click(object sender, RoutedEventArgs e)
		{
			var content = $"TIME Now {DateTime.Now.Second}";

			await _container.AddDistinctAsync(content);
		}

		private async void RetrieveButton_Click(object sender, RoutedEventArgs e)
		{
			foreach (string content in await _container.RetrieveAsync())
			{
				//Debug.WriteLine($"{content}");
			}
		}
	}
}
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ActiproSoftware.Text;
using ActiproSoftware.Text.Implementation;
using Raven.Abstractions.Data;
using Raven.Json.Linq;
using Raven.Studio.Controls.Editors;
using Raven.Studio.Extensions;
using Raven.Studio.Infrastructure;
using Raven.Studio.Models;

namespace Raven.Studio.Features.Settings
{
	public class ScriptedIndexSettingsSectionModel : SettingsSectionModel
	{
		private static readonly ISyntaxLanguage JScriptLanguage;

		public BindableCollection<string> AvailableIndexes { get; private set; }
		private Observable<DatabaseModel> Database { get { return ApplicationModel.Database; } }
		public Dictionary<string, ScriptedIndexResults> ScriptedIndexes { get; private set; }
		private ScriptedIndexResults SelectedScript { get; set; }

		public EditorDocument IndexScript { get; private set; }
		public EditorDocument DeleteScript { get; private set; }

		static ScriptedIndexSettingsSectionModel()
		{
			JScriptLanguage = SyntaxEditorHelper.LoadLanguageDefinitionFromResourceStream("JScript.langdef");
		}

		public ScriptedIndexSettingsSectionModel()
		{
			AvailableIndexes = new BindableCollection<string>(x => x);
			SectionName = "Scripted Index";			
			ScriptedIndexes = new Dictionary<string, ScriptedIndexResults>();
			IndexScript = new EditorDocument { Language = JScriptLanguage };
			DeleteScript = new EditorDocument { Language = JScriptLanguage };
			UpdateAvailableIndexes();
			LoadScrtipForIndex();
		}

		private void LoadScrtipForIndex()
		{
			if (IndexName == null)
				return;
			
			if (ScriptedIndexes.ContainsKey(indexName))
			{
				SelectedScript = ScriptedIndexes[indexName];

				IndexScript.SetText(SelectedScript.IndexScript);
				DeleteScript.SetText(SelectedScript.DeleteScript);
				OnPropertyChanged(() => SelectedScript);
				OnPropertyChanged(() => IndexScript);
				OnPropertyChanged(() => DeleteScript);
				return;
			}

			var id = ScriptedIndexResults.IdPrefix + indexName;
			ApplicationModel.DatabaseCommands.GetAsync(id)
			                .ContinueOnSuccessInTheUIThread(doc =>
			                {
				                if (doc == null)
				                {
					                ScriptedIndexes[indexName] = new ScriptedIndexResults {Id = id};
				                }
				                else
				                {
					                ScriptedIndexes[indexName] =
						                ApplicationModel.CreateSerializer()
						                                .Deserialize<ScriptedIndexResults>(new RavenJTokenReader(doc.DataAsJson));
				                }

				                SelectedScript = ScriptedIndexes[indexName];

				                IndexScript.SetText(SelectedScript.IndexScript);
				                DeleteScript.SetText(SelectedScript.DeleteScript);
				                OnPropertyChanged(() => SelectedScript);
								OnPropertyChanged(() => IndexScript);
								OnPropertyChanged(() => DeleteScript);
			                });
		}

		private string indexName;
		public string IndexName
		{
			get
			{
				return indexName;
			}
			set
			{
				if (string.IsNullOrWhiteSpace(value))
					return;

				if (indexName != null)
					StoreChanges();

				indexName = value;
				LoadScrtipForIndex();
				OnPropertyChanged(() => IndexName);
			}
		}

		public void StoreChanges()
		{
			SelectedScript.IndexScript = IndexScript.Text;
			SelectedScript.DeleteScript = DeleteScript.Text;
		}

		protected override void OnViewLoaded()
		{
			base.OnViewLoaded();

			Database.ObservePropertyChanged()
						 .TakeUntil(Unloaded)
						 .Subscribe(_ =>
						 {
							 UpdateAvailableIndexes();

							 Database.Value.Statistics.ObservePropertyChanged()
									 .TakeUntil(
										 Database.ObservePropertyChanged().Select(__ => Unit.Default).Amb(Unloaded))
									 .Subscribe(__ => UpdateAvailableIndexes());
						 });

			UpdateAvailableIndexes();
		}

		private void UpdateAvailableIndexes()
		{
			if (Database.Value == null || Database.Value.Statistics.Value == null)
			{
				return;
			}

			AvailableIndexes.Match(Database.Value.Statistics.Value.Indexes.Select(i => i.Name).ToArray());
			if (IndexName == null)
				IndexName = AvailableIndexes.FirstOrDefault();
		}
	}
}

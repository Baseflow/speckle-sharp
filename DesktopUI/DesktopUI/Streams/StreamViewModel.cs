﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MaterialDesignThemes.Wpf;
using Speckle.Core.Api;
using Speckle.Core.Logging;
using Speckle.DesktopUI.Utils;
using Stylet;

namespace Speckle.DesktopUI.Streams
{
  public class StreamViewModel : Screen, IHandle<ApplicationEvent>, IHandle<StreamUpdatedEvent>
  {
    private readonly IEventAggregator _events;
    private readonly ViewManager _viewManager;
    private readonly IDialogFactory _dialogFactory;
    private readonly ConnectorBindings _bindings;
    private StreamsRepository _repo;

    private StreamState _streamState;
    public StreamState StreamState
    {
      get => _streamState;
      set
      {
        SetAndNotify(ref _streamState, value);

        Branch = StreamState.Stream.branches.items[0];
        DisplayName = "Stream Details";
        _StreamName = StreamState.Stream.name;
        _StreamDescription = StreamState.Stream.description;

        NotifyOfPropertyChange(nameof(LatestCommit));
      }
    }

    private Branch _branch;
    public Branch Branch
    {
      get => _branch;
      set => SetAndNotify(ref _branch, value);
    }

    private string _StreamName;
    public string StreamName
    {
      get => _StreamName;
      set
      {
        SetAndNotify(ref _StreamName, value);
        UpdateStreamNameOrDescription();
      }
    }

    private string _StreamDescription;
    public string StreamDescription
    {
      get => _StreamDescription;
      set
      {
        SetAndNotify(ref _StreamDescription, value);
        UpdateStreamNameOrDescription();
      }
    }

    public Commit LatestCommit
    {
      get => StreamState.LatestCommit(Branch.name);
    }

    private CancellationTokenSource _cancellationToken = new CancellationTokenSource();

    public StreamViewModel(IEventAggregator events, ViewManager viewManager, IDialogFactory dialogFactory, StreamsRepository streamsRepo, ConnectorBindings bindings)
    {
      _events = events;
      _viewManager = viewManager;
      _dialogFactory = dialogFactory;
      _repo = streamsRepo;
      _bindings = bindings;
      DisplayName = StreamState?.Stream.name;

      _events.Subscribe(this);
    }

    public async void UpdateStreamNameOrDescription()
    {
      try
      {
        if(_StreamName == "")
        {
          return;
        }

        var client = StreamState.Client;
        await client.StreamUpdate(new StreamUpdateInput { id = StreamState.Stream.id, name = _StreamName, description = _StreamDescription });
        ((RootViewModel)Parent).Notifications.Enqueue("Stream updated."); 
      }
      catch (Exception e)
      {
        ((RootViewModel)Parent).Notifications.Enqueue($"Failed to update stream.\nError: {e.Message}");
      }
    }

    public void RemoveStream()
    {
      Tracker.TrackPageview("stream", "remove");
      _bindings.RemoveStream(StreamState.Stream.id);
      _events.Publish(new StreamRemovedEvent() { StreamId = StreamState.Stream.id });
      RequestClose();
    }

    public async void DeleteStream()
    {
      Tracker.TrackPageview("stream", "delete");
      var deleted = await _repo.DeleteStream(StreamState);
      if (!deleted)
      {
        DialogHost.CloseDialogCommand.Execute(null, null);
        return;
      }

      _events.Publish(new StreamRemovedEvent() { StreamId = StreamState.Stream.id });
      DialogHost.CloseDialogCommand.Execute(null, null);
      RequestClose();
    }

    // TODO figure out how to call this from parent instead of
    // rewriting the method here
    public void CopyStreamId(string streamId)
    {
      Clipboard.SetText(streamId);
      _events.Publish(new ShowNotificationEvent() { Notification = "Stream ID copied to clipboard!" });
    }

    public void OpenStreamInWeb(StreamState state)
    {
      Tracker.TrackPageview("stream", "web");
      Link.OpenInBrowser($"{state.ServerUrl}/streams/{state.Stream.id}");
    }

    public void Handle(ApplicationEvent message)
    {
      switch (message.Type)
      {
        case ApplicationEvent.EventType.DocumentOpened:
        case ApplicationEvent.EventType.DocumentClosed:
          {
            RequestClose();
            break;
          }
        default:
          return;
      }
    }

    public void Handle(StreamUpdatedEvent message)
    {
      if (message.StreamId != StreamState.Stream.id) return;
      StreamState.Stream = message.Stream;
    }
  }
}

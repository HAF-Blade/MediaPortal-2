#region Copyright (C) 2007-2010 Team MediaPortal

/*
    Copyright (C) 2007-2010 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;

namespace MediaPortal.Core.Messaging
{
  /// <summary>
  /// Called when a system message arrived.
  /// </summary>
  /// <param name="message">Message which arrived.</param>
  /// <returns><c>true</c>, if the message could successfully be handled. In that case, the <see cref="MessageWatcher"/>
  /// will automatically dispose if its <see cref="MessageWatcher.AutoDispose"/> property is set. <c>false</c>, if
  /// the message could not be handled.</returns>
  public delegate bool MessageHandlerDlgt(SystemMessage message);

  /// <summary>
  /// Watcher class to react to queue messages from the <see cref="IMessageBroker"/>.
  /// </summary>
  /// <remarks>
  /// The caller must hold a strong reference to instances of this class, else it might be collected by the garbage collector.
  /// </remarks>
  public class MessageWatcher : IDisposable, IMessageReceiver
  {
    protected bool _registered = false;
    protected object _owner;
    protected string _messageChannel;
    protected MessageHandlerDlgt _handler;
    protected bool _autoDispose;

    public MessageWatcher(object owner, string messageChannel, MessageHandlerDlgt handler, bool autoDispose)
    {
      _owner = owner;
      _messageChannel = messageChannel;
      _handler = handler;
      _autoDispose = autoDispose;
      Register();
    }

    public void Dispose()
    {
      Unregister();
    }

    public bool AutoDispose
    {
      get { return _autoDispose; }
      set { _autoDispose = value; }
    }

    public void Register()
    {
      if (_registered)
        return;
      IMessageBroker broker = ServiceRegistration.Get<IMessageBroker>();
      broker.RegisterMessageReceiver(_messageChannel, this);
      _registered = true;
    }

    public void Unregister()
    {
      if (!_registered)
        return;
      IMessageBroker broker = ServiceRegistration.Get<IMessageBroker>();
      broker.UnregisterMessageReceiver(_messageChannel, this);
      _registered = false;
    }

    #region Implementation of IMessageReceiver

    public void Receive(SystemMessage message)
    {
      if (_handler(message) && _autoDispose)
        Dispose();
    }

    #endregion
  }
}
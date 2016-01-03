﻿/*  MyNetSensors 
    Copyright (C) 2015 Derwish <derwish.pro@gmail.com>
    License: http://www.gnu.org/licenses/gpl-3.0.txt  
*/

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using Microsoft.Data.Entity;
using MyNetSensors.Gateways;

namespace MyNetSensors.Repositories.EF.SQLite
{




    public class GatewayRepositoryEF : IGatewayRepository
    {
        private string connectionString;

        private bool showConsoleMessages = false;

        //if writeInterval==0, every message will be instantly writing to DB
        //and this will increase the reliability of the system, 
        //but this greatly slows down the performance.
        //If you set writeInterval>0, the state of all sensors 
        //will be writed to DB with this interval.
        //writeInterval should be large enough (3000 ms is ok)
        private int writeInterval = 5000;

        //slows down the performance, can cause to exception of a large flow of messages per second
        public bool storeTxRxMessages = false;

        private Gateway gateway;
        private Timer updateDbTimer = new Timer();

        //store id-s of updated nodes, to write to db by timer
        private List<int> updatedNodesId = new List<int>();
        //messages list, to write to db by timer
        private List<Message> newMessages = new List<Message>();

        private NodesDbContext db;

        public GatewayRepositoryEF(NodesDbContext nodesDbContext)
        {
            updateDbTimer.Elapsed += UpdateDbTimerEvent;

            this.db = nodesDbContext;
            CreateDb();
        }


        public void ConnectToGateway(Gateway gateway)
        {

            this.gateway = gateway;

            List<Message> messages = GetMessages();
            foreach (var message in messages)
                gateway.messagesLog.AddNewMessage(message);

            List<Node> nodes = GetNodes();
            foreach (var node in nodes)
                gateway.AddNode(node);

            gateway.messagesLog.OnNewMessageLogged += OnNewMessage;
            gateway.messagesLog.OnClearMessages += OnClearMessages;

            gateway.OnClearNodesListEvent += OnClearNodesListEvent;

            gateway.OnNewNodeEvent += OnNodeUpdated;
            gateway.OnNodeUpdatedEvent += OnNodeUpdated;
            gateway.OnNewSensorEvent += OnSensorUpdated;
            gateway.OnSensorUpdatedEvent += OnSensorUpdated;


            if (writeInterval > 0)
            {
                updateDbTimer.Interval = writeInterval;
                updateDbTimer.Start();
            }
        }


        public void CreateDb()
        {
            try
            {
                db.Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw ex;
            }
        }

        public void DropMessages()
        {
            db.Database.ExecuteSqlCommand("TRUNCATE TABLE [Messages]");
        }

        public void DropNodes()
        {
            db.Database.ExecuteSqlCommand("TRUNCATE TABLE [Nodes]");
            db.Database.ExecuteSqlCommand("TRUNCATE TABLE [Sensors]");
        }


        private void OnClearMessages()
        {
            DropMessages();
        }

        private void OnClearNodesListEvent()
        {
            DropNodes();
        }


        public List<Message> GetMessages()
        {
            return db.Messages.ToList();
        }

        private void OnNewMessage(Message message)
        {
            if (!storeTxRxMessages) return;

            if (writeInterval == 0)
                AddMessage(message);
            else
                newMessages.Add(message);
        }

        public void AddMessage(Message message)
        {
            db.Messages.Add(message);
            db.SaveChanges();
        }


 

        public List<Node> GetNodes()
        {
            return db.Nodes.Include(x => x.sensors).ToList();
        }


        public int AddOrUpdateNode(Node node)
        {
            int id = node.Id;

            Node oldNode = GetNodeByDbId(node.Id);

            if (oldNode == null)
                id = AddNode(node);
            else
                UpdateNode(node);

            return id;
        }

        public int AddNode(Node node)
        {
            //todo EF change nodeID automaticaly!!!!
            db.Nodes.Add(node);
            db.SaveChanges();
            gateway.SetNodeDbId(node.nodeId, node.Id);

            foreach (var sensor in node.sensors)
            {
                AddOrUpdateSensor(sensor);
            }

            return node.Id;
        }

        public void UpdateNode(Node node)
        {
            db.Nodes.Update(node);
            db.SaveChanges();

            foreach (var sensor in node.sensors)
            {
                AddOrUpdateSensor(sensor);
            }
        }

        public int AddOrUpdateSensor(Sensor sensor)
        {
            int id = sensor.Id;

            Sensor oldSensor = GetSensor(sensor.Id);

            if (oldSensor == null)
                id = AddSensor(sensor);
            else
                UpdateSensor(sensor);

            return id;
        }

        public int AddSensor(Sensor sensor)
        {
           // int node_Id = GetNodeByNodeId(sensor.nodeId).Id;

            db.Sensors.Add(sensor);
            db.SaveChanges();

            gateway.SetSensorDbId(sensor.nodeId, sensor.sensorId, sensor.Id);
            return sensor.Id;
        }

        public void UpdateSensor(Sensor sensor)
        {
            db.Sensors.Update(sensor);
            db.SaveChanges();
        }

        private void WriteNewMessages()
        {

            Message[] messages = new Message[newMessages.Count];
            newMessages.CopyTo(messages);
            newMessages.Clear();

            db.Messages.AddRange(messages);
            db.SaveChanges();
        }


        private void OnNodeUpdated(Node node)
        {
            if (writeInterval == 0) AddOrUpdateNode(node);
            else
            {
                if (!updatedNodesId.Contains(node.nodeId))
                    updatedNodesId.Add(node.nodeId);
            }
        }

        private void OnSensorUpdated(Sensor sensor)
        {
            if (writeInterval == 0) AddOrUpdateSensor(sensor);
            else
            {
                if (!updatedNodesId.Contains(sensor.nodeId))
                    updatedNodesId.Add(sensor.nodeId);
            }
        }

        private void WriteUpdatedNodes()
        {
            if (!updatedNodesId.Any()) return;

            //to prevent changing of collection while writing to db is not yet finished
            int[] nodesTemp = new int[updatedNodesId.Count];
            updatedNodesId.CopyTo(nodesTemp);
            updatedNodesId.Clear();

            List<Node> nodes = gateway.GetNodes();
            foreach (var id in nodesTemp)
            {
                Node node = nodes.FirstOrDefault(x => x.nodeId == id);
                AddOrUpdateNode(node);
            }
        }

        public bool IsDbExist()
        {
            //todo check if db exist
            return true;
        }

        public void ShowDebugInConsole(bool enable)
        {
            showConsoleMessages = enable;
        }



        public void SetStoreTxRxMessages(bool enable)
        {
            storeTxRxMessages = enable;
        }





        public Node GetNodeByNodeId(int nodeId)
        {
            return db.Nodes.FirstOrDefault(x => x.nodeId == nodeId);
        }


        public Node GetNodeByDbId(int id)
        {
            return db.Nodes.FirstOrDefault(x => x.Id == id);
        }

        public Sensor GetSensor(int id)
        {
            try
            {
                Sensor sensor = db.Sensors.FirstOrDefault(x => x.Id == id);
                return sensor;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public Sensor GetSensor(int nodeId, int sensorId)
        {
            return db.Sensors.FirstOrDefault(x => x.nodeId == nodeId && x.sensorId==sensorId);
        }


        public void UpdateNodeSettings(Node node)
        {
            Node oldNode = GetNodeByDbId(node.Id);
            oldNode.name = node.name;
            db.Nodes.Update(oldNode);
            db.SaveChanges();

            foreach (var sensor in node.sensors)
            {
                UpdateSensorSettings(sensor);
            }
        }

        public void UpdateSensorSettings(Sensor sensor)
        {
            Sensor oldSensor = GetSensor(sensor.Id);
            oldSensor.description = sensor.description;
            oldSensor.storeHistoryEnabled = sensor.storeHistoryEnabled;
            oldSensor.storeHistoryEveryChange = sensor.storeHistoryEveryChange;
            oldSensor.storeHistoryWithInterval = sensor.storeHistoryWithInterval;
            oldSensor.invertData = sensor.invertData;
            oldSensor.remapEnabled = sensor.remapEnabled;
            oldSensor.remapFromMin = sensor.remapFromMin;
            oldSensor.remapFromMax = sensor.remapFromMax;
            oldSensor.remapToMin = sensor.remapToMin;
            oldSensor.remapToMax = sensor.remapToMax;

            db.Sensors.Update(oldSensor);
            db.SaveChanges();
        }

        public void DeleteNodeByDbId(int id)
        {
            Node node = db.Nodes.FirstOrDefault(x => x.Id == id);
            if (node == null)
                return;

            db.Nodes.Remove(node);
            db.SaveChanges();

            List<Sensor> sensors = db.Sensors.Where(x => x.nodeId == node.Id).ToList();
            db.Sensors.RemoveRange(sensors);
            db.SaveChanges();
            
        }

        public void DeleteNodeByNodeId(int nodeId)
        {
            Node node = db.Nodes.FirstOrDefault(x => x.nodeId == nodeId);
            if (node == null)
                return;

            db.Nodes.Remove(node);
            db.SaveChanges();

            List<Sensor> sensors = db.Sensors.Where(x => x.nodeId == node.Id).ToList();
            db.Sensors.RemoveRange(sensors);
            db.SaveChanges();
        }







        private void UpdateDbTimerEvent(object sender, object e)
        {
            updateDbTimer.Stop();
            try
            {
                int nodesCount = updatedNodesId.Count;
                int messagesCount = newMessages.Count;
                int messages = nodesCount + messagesCount;
                if (messages == 0)
                {
                    updateDbTimer.Start();
                    return;
                };
                Stopwatch sw = new Stopwatch();
                sw.Start();


                WriteUpdatedNodes();
                WriteNewMessages();

                sw.Stop();
                long elapsed = sw.ElapsedMilliseconds;
                float messagesPerSec = (float)messages / (float)elapsed * 1000;
                Log($"Writing to DB: {elapsed} ms ({messages} inserts, {(int)messagesPerSec} inserts/sec)");
            }
            catch { }

            updateDbTimer.Start();
        }

        public void SetWriteInterval(int ms)
        {
            writeInterval = ms;
            updateDbTimer.Stop();
            if (writeInterval > 0)
            {
                updateDbTimer.Interval = writeInterval;
                if (gateway != null)
                    updateDbTimer.Start();
            }
        }

        public void Log(string message)
        {
            if (showConsoleMessages)
                Console.WriteLine(message);
        }

    }



}

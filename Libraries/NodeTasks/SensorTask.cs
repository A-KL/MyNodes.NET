﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MyNetSensors.Gateway;
using Newtonsoft.Json;

namespace MyNetSensors.NodeTasks
{
    public class SensorTask
    {
        [Key]
        public int db_Id { get; set; }
        public string description { get; set; }
        public int nodeId { get; set; }
        public int sensorId { get; set; }
        public int sensorDbId { get; set; }
        public DateTime executionDate { get; set; }
        public SensorDataType? dataType { get; set; }
        public string executionValue { get; set; }
        public bool isCompleted { get; set; }

        public bool isRepeating { get; set; }
        public int repeatingInterval { get; set; }
        public string repeatingAValue { get; set; }
        public string repeatingBValue { get; set; }
        //if repeatingCount==0, then will run indefinitely
        public int repeatingCount { get; set; }
        public int executionsDoneCount { get; set; }

        public SensorData GetExecutionSensorData()
        {
            return new SensorData(dataType,executionValue);
        }

        public SensorData GetRepeatingASensorData()
        {
            return new SensorData(dataType, repeatingAValue);
        }

        public SensorData GetRepeatingBSensorData()
        {
            return new SensorData(dataType, repeatingBValue);
        }

        public void SetExecutionValue(SensorData data)
        {
            executionValue = data.state;
            dataType = data.dataType;
        }

        public void SetRepeatingAValue(SensorData data)
        {
            repeatingAValue = data.state;
            dataType = data.dataType;
        }

        public void SetRepeatingBValue(SensorData data)
        {
            repeatingBValue = data.state;
            dataType = data.dataType;
        }
    }
}

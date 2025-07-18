using System.Text;
using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.pizerocamera.manager.Requests;
using picamerasserver.pizerocamera.Responses;

namespace picamerasserver.pizerocamera.manager;

public partial class PiZeroCameraManager
{
    /// <summary>
    /// Request to take a picture
    /// </summary>
    /// <param name="ids">Provide a List for specific devices, null for global</param>
    public async Task RequestTakePicture(IEnumerable<string>? ids)
    {
        var options = _optionsMonitor.CurrentValue;

        CameraRequest takePictureRequest = new CameraRequest.TakePicture(
            DateTime.Now.ToUnixTimeMilliSeconds(),
            Guid.CreateVersion7());

        if (ids == null)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithContentType("application/json")
                .WithTopic(options.CameraTopic)
                .WithPayload(Json.Serialize(takePictureRequest))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();

            var publishResult = await _mqttClient.PublishAsync(message);

            if (publishResult.IsSuccess)
            {
                foreach (var piZeroCamera in PiZeroCameras.Values)
                {
                    piZeroCamera.TakePictureRequest = new PiZeroCameraTakePictureRequest.Requested();
                }
            }
            else
            {
                foreach (var piZeroCamera in PiZeroCameras.Values)
                {
                    piZeroCamera.TakePictureRequest =
                        new PiZeroCameraTakePictureRequest.FailedToRequest(publishResult.ReasonString);
                }
            }

            OnChange?.Invoke();
            return;
        }

        MqttApplicationMessage GetMessage(string id) =>
            new MqttApplicationMessageBuilder().WithContentType("application/json")
                .WithTopic($"{options.CameraTopic}/{id}")
                .WithPayload(Json.Serialize(takePictureRequest))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();

        var idList = ids.ToList();

        // PiZeroCameras that are not requested set to null
        foreach (var id in PiZeroCameras.Keys.Except(idList))
        {
            PiZeroCameras[id].TakePictureRequest = null;
        }

        // PiZeroCameras that are requested
        var tasks = PiZeroCameras.Keys
            .Intersect(idList)
            .Select(async id => (id, await _mqttClient.PublishAsync(GetMessage(id))));


        var tasksResult = await Task.WhenAll(tasks);

        // Process message sending results
        foreach (var (id, publishResult) in tasksResult)
        {
            var piZeroCamera = PiZeroCameras[id];
            if (publishResult.IsSuccess)
            {
                piZeroCamera.TakePictureRequest = new PiZeroCameraTakePictureRequest.Requested();
            }
            else
            {
                piZeroCamera.TakePictureRequest =
                    new PiZeroCameraTakePictureRequest.FailedToRequest(publishResult.ReasonString);
            }
        }

        OnChange?.Invoke();
    }

    public void ResponseTakePicture(MqttApplicationMessage message)
    {
        var id = message.Topic.Split('/').Last();

        var piZeroCamera = PiZeroCameras[id];

        var text = Encoding.UTF8.GetString(message.Payload);

        var response = Json.TryDeserialize<SuccessWrapper<TakePictureResponse>>(text, _logger);

        if (response.IsSuccess)
        {
            var successWrapper = response.Value;
            if (successWrapper.Success)
            {
                piZeroCamera.TakePictureRequest = successWrapper.Value switch
                {
                    TakePictureResponse.PictureSavedOnDevice =>
                        new PiZeroCameraTakePictureRequest.SavedOnDevice(),
                    TakePictureResponse.PictureSent => new PiZeroCameraTakePictureRequest.Success(),
                    _ => new PiZeroCameraTakePictureRequest.Unknown("Unknown success")
                };
            }
            else
            {
                piZeroCamera.TakePictureRequest = successWrapper.Value switch
                {
                    TakePictureResponse.Failure failure =>
                        new PiZeroCameraTakePictureRequest.Failure(failure),
                    _ => new PiZeroCameraTakePictureRequest.Unknown("Unknown failure")
                };
            }
        }
        else
        {
            piZeroCamera.TakePictureRequest = new PiZeroCameraTakePictureRequest.Unknown(response.Error.ToString());
        }

        OnChange?.Invoke();
    }
}
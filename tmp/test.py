from azure.cognitiveservices.vision.computervision import ComputerVisionClient
from azure.cognitiveservices.vision.computervision.models import OperationStatusCodes
from azure.cognitiveservices.vision.computervision.models import VisualFeatureTypes
from msrest.authentication import CognitiveServicesCredentials
 
from array import array
import os
from PIL import Image
import sys
import time
import cv2 
from io import BytesIO
 
subscription_key = "74d7f4aec4ce4b97a32bac9636d85717"
endpoint = "https://meuzz-cameraapp.cognitiveservices.azure.com/"
# endpoint = "https://japanwest.api.cognitive.microsoft.com/"
 
computervision_client = ComputerVisionClient(endpoint, CognitiveServicesCredentials(subscription_key))
 
print("===== Batch Read File - remote =====")
remote_image_handw_text_url = "https://raw.githubusercontent.com/MicrosoftDocs/azure-docs/master/articles/cognitive-services/Computer-vision/Images/readsample.jpg"
 
recognize_handw_results = computervision_client.read(remote_image_handw_text_url, raw=True)
operation_location_remote = recognize_handw_results.headers["Operation-Location"]
operation_id = operation_location_remote.split("/")[-1]
 
while True:
    get_handw_text_results = computervision_client.get_read_result(operation_id)
    if get_handw_text_results.status not in ['notStarted', 'running']:
        break
    time.sleep(1)
 
if get_handw_text_results.status == OperationStatusCodes.succeeded:
    for text_result in get_handw_text_results.analyze_result.read_results:
        for line in text_result.lines:
            print(line.text)
            print(line.bounding_box)
print()

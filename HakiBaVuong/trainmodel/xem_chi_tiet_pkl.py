# xem_chi_tiet_pkl.py — copy vào 1 file rồi chạy
import pickle

with open('lightfm_model.pkl', 'rb') as f:
    data = pickle.load(f)

model = data['model']
dataset = data['dataset']

print("MODEL ĐÃ LOAD THÀNH CÔNG!")
print(f"→ Số user: {model.user_embeddings.shape[0]}")
print(f"→ Số item: {model.item_embeddings.shape[0]}")
print(f"→ Loss function: {model.loss}")
print(f"→ Số epochs đã train: {model.fit_calls}")
print("→ File .pkl HOÀN HẢO – DÙNG ĐƯỢC NGAY TRONG .NET!")